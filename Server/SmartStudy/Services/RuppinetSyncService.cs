using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class RuppinetSyncService
{
    private readonly DBservices _dal;
    private readonly RuppinetApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<RuppinetSyncService> _logger;
    private readonly SchedulingService _scheduling;

    public RuppinetSyncService(DBservices dal, RuppinetApiClient api,
        IConfiguration config, ILogger<RuppinetSyncService> logger, SchedulingService scheduling)
    {
        _dal = dal;
        _api = api;
        _config = config;
        _logger = logger;
        _scheduling = scheduling;
    }

    public async Task<RuppinetSyncResultDto> SyncAllAsync(string email)
    {
        var result = new RuppinetSyncResultDto();
        var user = await _dal.GetUserByEmailAsync(email);
        if (user == null || string.IsNullOrEmpty(user.RuppinetId) || string.IsNullOrEmpty(user.RuppinetPassword))
        {
            result.Message = "Ruppinet credentials not configured";
            return result;
        }

        var zht = user.RuppinetId;
        string password;
        try
        {
            password = DecryptPassword(user.RuppinetPassword);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            _logger.LogWarning(ex, "Failed to decrypt Ruppinet password for {Email}", email);
            result.Message = "Failed to decrypt credentials. Please reconnect your Ruppinet account.";
            result.ErrorCode = "DECRYPT_FAILED";
            return result;
        }

        _logger.LogInformation("Starting Ruppinet sync for {Email}", email);

        string? token;
        try
        {
            token = await _api.LoginAsync(zht, password);
        }
        catch (RuppinetApiException ex)
        {
            _logger.LogWarning(ex, "Ruppinet auth failed for {Email}", email);
            result.Message = ex.Message;
            result.ErrorCode = ex.ErrorCode;
            return result;
        }

        if (string.IsNullOrEmpty(token))
        {
            result.Message = "Failed to authenticate with Ruppinet";
            result.ErrorCode = "AUTH_FAILED";
            return result;
        }

        try
        {
            var scheduleFetchDays = int.TryParse(_config["Ruppinet:ScheduleFetchDays"], out var d) ? d : 120;
            var from = DateTime.Now.Date;
            var to = from.AddDays(scheduleFetchDays);

            var coursesTask = _api.GetCoursesAsync(token);
            var scheduleTask = _api.GetScheduleAsync(token, from, to);
            var examsTask = _api.GetExamsAsync(token);

            await Task.WhenAll(coursesTask, scheduleTask, examsTask);

            var ruppinetCourses = await coursesTask;
            var scheduleEvents = await scheduleTask;
            var ruppinetExams = await examsTask;

            await ProcessCoursesAsync(email, ruppinetCourses, result);
            await CleanupDuplicateCoursesAsync(email);
            await ProcessScheduleAsync(email, scheduleEvents, result);
            await ProcessExamsAsync(email, ruppinetExams, result);

            await _dal.UpdateLastRuppinetSyncAsync(email, DateTime.UtcNow);

            // Run the scheduling engine to place tasks into calendar slots
            await _scheduling.ScheduleAllTasksAsync(email);

            result.Success = true;
            result.Message = $"Synced {result.CoursesCreated} new courses, {result.ClassEventsCreated} class events, {result.ExamsCreated} exams";
            _logger.LogInformation("Ruppinet sync completed for {Email}: {Message}", email, result.Message);
        }
        catch (RuppinetApiException ex)
        {
            _logger.LogWarning(ex, "Ruppinet API error during sync for {Email}", email);
            result.Message = $"Sync error: {ex.Message}";
            result.ErrorCode = ex.ErrorCode;
            result.Warnings.Add(ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during Ruppinet sync for {Email}", email);
            result.Message = $"Sync error: {ex.Message}";
            result.ErrorCode = "API_ERROR";
            result.Warnings.Add(ex.ToString());
        }

        return result;
    }

    public async Task<bool> TestConnectionAsync(string zht, string password)
    {
        try
        {
            var token = await _api.LoginAsync(zht, password);
            return !string.IsNullOrEmpty(token);
        }
        catch (RuppinetApiException)
        {
            return false;
        }
    }

    private async Task ProcessCoursesAsync(string email, List<RuppinetCourse> ruppinetCourses, RuppinetSyncResultDto result)
    {
        foreach (var rc in ruppinetCourses)
        {
            try
            {
                var courseId = rc.Nmrtr;
                if (courseId == 0) continue;

                var course = await _dal.GetCourseByIdAsync(courseId);
                if (course == null)
                {
                    var instructorId = await FindOrCreateInstructorAsync(rc.Instructors);
                    await _dal.CreateCourseAsync(courseId, Truncate(rc.Name, 200), rc.WeeklyHours, rc.Credits,
                        TruncateNullable($"{rc.Semester}-{rc.SemesterCode}", 50), instructorId);
                    result.CoursesCreated++;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(rc.Name))
                        await _dal.UpdateCourseAsync(courseId, courseName: Truncate(rc.Name, 200),
                            credits: rc.Credits > 0 ? rc.Credits : null,
                            weeklyHours: rc.WeeklyHours > 0 ? rc.WeeklyHours : null);
                    result.CoursesUpdated++;
                }

                if (!await _dal.UserCourseExistsAsync(email, courseId))
                {
                    await _dal.CreateUserCourseAsync(email, courseId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Course sync error for {Code}", rc.Code);
                result.Warnings.Add($"Course sync error ({rc.Code}): {ex.Message}");
            }
        }
    }

    private async Task ProcessScheduleAsync(string email, List<RuppinetScheduleEvent> events, RuppinetSyncResultDto result)
    {
        var courseMap = await BuildCourseMapAsync(email);

        foreach (var evt in events)
        {
            try
            {
                var eventFrom = evt.Date.Date + evt.StartTime;
                var eventTo = evt.Date.Date + evt.EndTime;
                if (eventFrom >= eventTo) continue;

                var courseId = FindCourseId(evt.Title, courseMap);
                if (courseId == null)
                {
                    courseId = await CreateCourseFromSchedule(email, evt);
                    if (courseId != null)
                        courseMap[evt.Title.ToLower().Trim()] = courseId.Value;
                    else
                        continue;
                }

                if (await _dal.ClassEventExistsAsync(email, courseId.Value, eventFrom, eventTo))
                {
                    result.ClassEventsSkipped++;
                    continue;
                }

                var duration = (decimal)(eventTo - eventFrom).TotalHours;
                await _dal.CreateClassEventAsync(email, eventFrom, eventTo, false, null,
                    courseId.Value, TruncateNullable(evt.Location, 200), duration);
                result.ClassEventsCreated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Schedule sync error for {Title}", evt.Title);
                result.Warnings.Add($"Schedule sync error ({evt.Title}): {ex.Message}");
            }
        }
    }

    private async Task ProcessExamsAsync(string email, List<RuppinetExam> ruppinetExams, RuppinetSyncResultDto result)
    {
        var courseMap = await BuildCourseMapAsync(email);

        foreach (var re in ruppinetExams)
        {
            try
            {
                var courseId = re.CourseNmrtr > 0 ? re.CourseNmrtr : FindCourseId(re.CourseName, courseMap);
                if (courseId == null)
                {
                    var id = await CreateCourseFromExam(email, re);
                    if (id == null) continue;
                    courseId = id;
                }
                else
                {
                    if (!await _dal.CourseExistsAsync(courseId.Value))
                    {
                        var id = await CreateCourseFromExam(email, re);
                        if (id == null) continue;
                        courseId = id;
                    }
                }

                var session = re.MoedMis switch
                {
                    1 => "A",
                    2 => "B",
                    3 => "C",
                    _ => "A"
                };

                var existingExam = await _dal.FindExamByCourseAndSessionAsync(email, courseId.Value, session);

                if (existingExam != null)
                {
                    if (existingExam.Date != re.Date.Date)
                    {
                        _logger.LogWarning("Exam date changed for course {CourseId} session {Session}: {OldDate} -> {NewDate}",
                            courseId.Value, session, existingExam.Date.ToString("yyyy-MM-dd"), re.Date.Date.ToString("yyyy-MM-dd"));
                    }
                    await _dal.UpdateExamFullAsync(existingExam.ExamId, re.Date.Date, re.StartTime, (int)(re.DurationHours * 60));
                    result.ExamsUpdated++;
                }
                else
                {
                    await _dal.CreateExamAsync(courseId.Value, re.Date.Date, re.StartTime, session,
                        (int)(re.DurationHours * 60), true);
                    result.ExamsCreated++;
                }

                if (re.DurationEstimated)
                {
                    _logger.LogWarning("Exam duration estimated (3h default) for {CourseName} session {Session}",
                        re.CourseName, session);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exam sync error for {CourseName}", re.CourseName);
                result.Warnings.Add($"Exam sync error ({re.CourseName}): {ex.Message}");
            }
        }
    }

    private async Task<int?> CreateCourseFromExam(string email, RuppinetExam re)
    {
        var courseId = re.CourseNmrtr > 0 ? re.CourseNmrtr : await GenerateStableIdAsync(re.CourseName, 0);

        var existingByName = await FindCourseByNameAsync(re.CourseName);
        if (existingByName != null)
        {
            if (!await _dal.UserCourseExistsAsync(email, existingByName.CourseId))
                await _dal.CreateUserCourseAsync(email, existingByName.CourseId);
            return existingByName.CourseId;
        }

        if (!await _dal.CourseExistsAsync(courseId))
        {
            var instructorId = await FindOrCreateInstructorAsync(re.Instructor);
            await _dal.CreateCourseAsync(courseId, Truncate(re.CourseName, 200), null, null,
                TruncateNullable(re.Semester, 50), instructorId);
        }

        if (!await _dal.UserCourseExistsAsync(email, courseId))
            await _dal.CreateUserCourseAsync(email, courseId);

        return courseId;
    }

    private async Task<int?> CreateCourseFromSchedule(string email, RuppinetScheduleEvent evt)
    {
        var name = evt.Title.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var existingByName = await FindCourseByNameAsync(name);
        if (existingByName != null)
        {
            if (!await _dal.UserCourseExistsAsync(email, existingByName.CourseId))
                await _dal.CreateUserCourseAsync(email, existingByName.CourseId);
            return existingByName.CourseId;
        }

        var courseId = await GenerateStableIdAsync(name, 200000);
        if (!await _dal.CourseExistsAsync(courseId))
        {
            var instructorId = await FindOrCreateInstructorAsync(evt.Instructor);
            await _dal.CreateCourseAsync(courseId, Truncate(name, 200), null, null, null, instructorId);
        }

        if (!await _dal.UserCourseExistsAsync(email, courseId))
            await _dal.CreateUserCourseAsync(email, courseId);

        return courseId;
    }

    private async Task<int> GenerateStableIdAsync(string input, int offset)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLower().Trim()));
        var value = Math.Abs(BitConverter.ToInt32(hash, 0)) % 90000000 + 10000000 + offset;

        for (int i = 0; i < 100; i++)
        {
            var existing = await _dal.GetCourseByIdAsync(value);
            if (existing == null || existing.CourseName.ToLower().Trim() == input.ToLower().Trim())
                return value;
            value++;
        }
        return value;
    }

    private async Task<Dictionary<string, int>> BuildCourseMapAsync(string email)
    {
        var userCourses = await _dal.GetUserCoursesWithNameAsync(email);
        var map = new Dictionary<string, int>();
        foreach (var (courseId, courseName) in userCourses)
        {
            var key = NormalizeName(courseName);
            map.TryAdd(key, courseId);
        }
        return map;
    }

    private static string NormalizeName(string name)
    {
        return name.ToLower().Trim()
            .Replace(')', '(')
            .Replace('(', '(')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"');
    }

    private static int? FindCourseId(string title, Dictionary<string, int> courseMap)
    {
        var normalized = NormalizeName(title);
        if (courseMap.TryGetValue(normalized, out var id)) return id;

        int? bestMatch = null;
        int bestLength = 0;

        foreach (var (name, courseId) in courseMap)
        {
            if (normalized.Contains(name) && name.Length > bestLength)
            {
                bestMatch = courseId;
                bestLength = name.Length;
            }
            else if (name.Contains(normalized) && normalized.Length > bestLength)
            {
                bestMatch = courseId;
                bestLength = normalized.Length;
            }
        }

        if (bestMatch != null) return bestMatch;

        var splitChars = new[] { ' ', '-', '(', ')', '\u0029', '\u0028' };
        var titleWords = normalized.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
        foreach (var (name, courseId) in courseMap)
        {
            var nameWords = name.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
            if (nameWords.Length < 2 || titleWords.Length < 2) continue;

            var wordsToMatch = Math.Min(nameWords.Length, Math.Min(titleWords.Length, 3));
            var matched = true;
            var totalMatchedChars = 0;
            for (int i = 0; i < wordsToMatch; i++)
            {
                if (!titleWords[i].StartsWith(nameWords[i]) && !nameWords[i].StartsWith(titleWords[i]))
                {
                    matched = false;
                    break;
                }
                totalMatchedChars += Math.Min(titleWords[i].Length, nameWords[i].Length);
            }
            if (matched && wordsToMatch >= 2 && totalMatchedChars >= 4)
                return courseId;
        }

        return null;
    }

    private async Task<Course?> FindCourseByNameAsync(string name)
    {
        var normalized = NormalizeName(name);
        var candidates = await _dal.GetAllCoursesAsync();
        return candidates.FirstOrDefault(c => NormalizeName(c.CourseName) == normalized);
    }

    private async Task CleanupDuplicateCoursesAsync(string email)
    {
        var userCourses = await _dal.GetUserCoursesWithNameAsync(email);

        var groups = userCourses.GroupBy(uc => NormalizeName(uc.CourseName)).ToList();

        foreach (var group in groups)
        {
            if (group.Count() <= 1) continue;

            var ordered = group.OrderBy(uc => uc.CourseId).ToList();
            var keepCourseId = ordered.First().CourseId;
            var duplicateIds = ordered.Skip(1).Select(uc => uc.CourseId).ToList();

            foreach (var dupId in duplicateIds)
            {
                _logger.LogInformation("Merging duplicate course {DupId} into {KeepId} for {Email}",
                    dupId, keepCourseId, email);

                await _dal.ReassignClassEventsCourseAsync(email, dupId, keepCourseId);
                await _dal.ReassignExamsCourseAsync(dupId, keepCourseId);
                await _dal.ReassignTasksCourseAsync(email, dupId, keepCourseId);

                await _dal.DeleteUserCourseAsync(email, dupId);

                if (!await _dal.OtherUsersEnrolledAsync(dupId, email))
                {
                    await _dal.DeleteCourseAsync(dupId);
                }
            }
        }
    }

    private async Task<int?> FindOrCreateInstructorAsync(string? instructorNames)
    {
        if (string.IsNullOrWhiteSpace(instructorNames)) return null;

        var name = instructorNames.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .First().Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var truncatedName = Truncate(name, 200);
        var instructor = await _dal.FindInstructorByNameAsync(truncatedName);
        if (instructor != null) return instructor.InstructorId;

        return await _dal.CreateInstructorAsync(truncatedName);
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? "" : value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateNullable(string? value, int maxLength)
        => value == null ? null : value.Length <= maxLength ? value : value[..maxLength];

    public string EncryptPassword(string plainText)
    {
        var key = GetEncryptionKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    private string DecryptPassword(string cipherText)
    {
        var key = GetEncryptionKey();
        var fullCipher = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
    }

    private byte[] GetEncryptionKey()
    {
        var configKey = _config["Ruppinet:EncryptionKey"] ?? "SmartStudyRuppinetEncKey2026!aa";
        using var sha = System.Security.Cryptography.SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(configKey));
    }
}
