using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class RuppinetSyncService
{
    private readonly SmartStudyDbContext _db;
    private readonly RuppinetApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<RuppinetSyncService> _logger;

    public RuppinetSyncService(SmartStudyDbContext db, RuppinetApiClient api,
        IConfiguration config, ILogger<RuppinetSyncService> logger)
    {
        _db = db;
        _api = api;
        _config = config;
        _logger = logger;
    }

    public async Task<RuppinetSyncResultDto> SyncAllAsync(string email)
    {
        var result = new RuppinetSyncResultDto();
        var user = await _db.Users.FindAsync(email);
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
            // Phase 4A: Fetch all data from Ruppinet API in parallel
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

            // Process sequentially: courses first (schedule/exams need the course map)
            await ProcessCoursesAsync(email, ruppinetCourses, result);
            await ProcessScheduleAsync(email, scheduleEvents, result);
            await ProcessExamsAsync(email, ruppinetExams, result);

            user.LastRuppinetSync = DateTime.UtcNow;
            await _db.SaveChangesAsync();

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

                var course = await _db.Courses.FindAsync(courseId);
                if (course == null)
                {
                    var instructorId = await FindOrCreateInstructorAsync(rc.Instructors);
                    course = new Course
                    {
                        CourseId = courseId,
                        CourseName = rc.Name,
                        Credits = rc.Credits,
                        WeeklyHours = rc.WeeklyHours,
                        Semester = $"{rc.Semester}-{rc.SemesterCode}",
                        InstructorId = instructorId
                    };
                    _db.Courses.Add(course);
                    result.CoursesCreated++;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(rc.Name))
                        course.CourseName = rc.Name;
                    if (rc.Credits > 0)
                        course.Credits = rc.Credits;
                    if (rc.WeeklyHours > 0)
                        course.WeeklyHours = rc.WeeklyHours;
                    result.CoursesUpdated++;
                }

                var enrolled = await _db.UserCourses
                    .AnyAsync(uc => uc.Email == email && uc.CourseId == courseId);
                if (!enrolled)
                {
                    _db.UserCourses.Add(new UserCourse { Email = email, CourseId = courseId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Course sync error for {Code}", rc.Code);
                result.Warnings.Add($"Course sync error ({rc.Code}): {ex.Message}");
            }
        }
        // Batch save after all courses
        await _db.SaveChangesAsync();
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

                var exists = await _db.ClassEvents
                    .AnyAsync(ce => ce.Email == email
                        && ce.CourseId == courseId.Value
                        && ce.From == eventFrom
                        && ce.To == eventTo);

                if (exists)
                {
                    result.ClassEventsSkipped++;
                    continue;
                }

                var duration = (decimal)(eventTo - eventFrom).TotalHours;
                _db.ClassEvents.Add(new ClassEvent
                {
                    Email = email,
                    From = eventFrom,
                    To = eventTo,
                    Recurring = false,
                    CourseId = courseId.Value,
                    Location = evt.Location,
                    Duration = duration
                });
                result.ClassEventsCreated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Schedule sync error for {Title}", evt.Title);
                result.Warnings.Add($"Schedule sync error ({evt.Title}): {ex.Message}");
            }
        }
        // Batch save after all schedule events
        await _db.SaveChangesAsync();
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
                    var courseExists = await _db.Courses.AnyAsync(c => c.CourseId == courseId.Value);
                    if (!courseExists)
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

                // Phase 2D: Dedup by (CourseId, Session) to handle moved exams
                var existingExam = await _db.Exams
                    .FirstOrDefaultAsync(e => e.CourseId == courseId.Value
                        && e.Session == session);

                if (existingExam != null)
                {
                    if (existingExam.Date != re.Date.Date)
                    {
                        _logger.LogWarning("Exam date changed for course {CourseId} session {Session}: {OldDate} -> {NewDate}",
                            courseId.Value, session, existingExam.Date.ToString("yyyy-MM-dd"), re.Date.Date.ToString("yyyy-MM-dd"));
                    }
                    existingExam.Date = re.Date.Date;
                    existingExam.Time = re.StartTime;
                    existingExam.Duration = re.DurationHours * 60;
                    result.ExamsUpdated++;
                }
                else
                {
                    _db.Exams.Add(new Exam
                    {
                        CourseId = courseId.Value,
                        Date = re.Date.Date,
                        Time = re.StartTime,
                        Session = session,
                        Duration = re.DurationHours * 60,
                        IsTakingExam = true
                    });
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
        // Batch save after all exams
        await _db.SaveChangesAsync();
    }

    private async Task<int?> CreateCourseFromExam(string email, RuppinetExam re)
    {
        var courseId = re.CourseNmrtr > 0 ? re.CourseNmrtr : GenerateStableId(re.CourseName, 0);

        // Check for existing course by name to avoid duplicates with old hash-based IDs
        var existingByName = await _db.Courses
            .FirstOrDefaultAsync(c => c.CourseName.ToLower() == re.CourseName.ToLower().Trim());
        if (existingByName != null)
        {
            // Ensure enrollment
            var enrolled = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == existingByName.CourseId);
            if (!enrolled)
                _db.UserCourses.Add(new UserCourse { Email = email, CourseId = existingByName.CourseId });
            return existingByName.CourseId;
        }

        var course = await _db.Courses.FindAsync(courseId);
        if (course == null)
        {
            var instructorId = await FindOrCreateInstructorAsync(re.Instructor);
            course = new Course
            {
                CourseId = courseId,
                CourseName = re.CourseName,
                Semester = re.Semester,
                InstructorId = instructorId
            };
            _db.Courses.Add(course);
        }

        var enrolledCheck = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == courseId);
        if (!enrolledCheck)
            _db.UserCourses.Add(new UserCourse { Email = email, CourseId = courseId });

        return courseId;
    }

    private async Task<int?> CreateCourseFromSchedule(string email, RuppinetScheduleEvent evt)
    {
        var name = evt.Title.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        // Check for existing course by name first
        var existingByName = await _db.Courses
            .FirstOrDefaultAsync(c => c.CourseName.ToLower() == name.ToLower());
        if (existingByName != null)
        {
            var enrolled = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == existingByName.CourseId);
            if (!enrolled)
                _db.UserCourses.Add(new UserCourse { Email = email, CourseId = existingByName.CourseId });
            return existingByName.CourseId;
        }

        var courseId = GenerateStableId(name, 200000);
        var existing = await _db.Courses.FindAsync(courseId);
        if (existing == null)
        {
            var instructorId = await FindOrCreateInstructorAsync(evt.Instructor);
            _db.Courses.Add(new Course
            {
                CourseId = courseId,
                CourseName = name,
                InstructorId = instructorId
            });
        }

        var enrolledCheck = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == courseId);
        if (!enrolledCheck)
            _db.UserCourses.Add(new UserCourse { Email = email, CourseId = courseId });

        return courseId;
    }

    /// <summary>
    /// Generate a deterministic ID from a string using SHA256 instead of GetHashCode.
    /// </summary>
    private static int GenerateStableId(string input, int offset)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLower().Trim()));
        var value = Math.Abs(BitConverter.ToInt32(hash, 0)) % 90000000 + 10000000 + offset;
        return value;
    }

    private async Task<Dictionary<string, int>> BuildCourseMapAsync(string email)
    {
        return await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Include(uc => uc.Course)
            .ToDictionaryAsync(uc => uc.Course.CourseName.ToLower().Trim(), uc => uc.CourseId);
    }

    private static int? FindCourseId(string title, Dictionary<string, int> courseMap)
    {
        var normalized = title.ToLower().Trim();
        if (courseMap.TryGetValue(normalized, out var id)) return id;

        // Phase 2B: Substring containment with best-match-length prioritization
        int? bestMatch = null;
        int bestLength = 0;

        foreach (var (name, courseId) in courseMap)
        {
            // Direct substring match — prefer longest matching name
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

        // Prefix-based matching with minimum 4-char guard
        var titleWords = normalized.Split(new[] { ' ', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var (name, courseId) in courseMap)
        {
            var nameWords = name.Split(new[] { ' ', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
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
            // Require at least 4 chars matched total and 2+ words
            if (matched && wordsToMatch >= 2 && totalMatchedChars >= 4)
                return courseId;
        }

        return null;
    }

    private async Task<int?> FindOrCreateInstructorAsync(string? instructorNames)
    {
        if (string.IsNullOrWhiteSpace(instructorNames)) return null;

        var name = instructorNames.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .First().Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var instructor = await _db.Instructors
            .FirstOrDefaultAsync(i => i.InstructorName == name);
        if (instructor != null) return instructor.InstructorId;

        instructor = new Instructor { InstructorName = name };
        _db.Instructors.Add(instructor);
        await _db.SaveChangesAsync();
        return instructor.InstructorId;
    }

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
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(configKey));
    }
}
