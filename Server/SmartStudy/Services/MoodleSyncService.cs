using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Services;

// Top-level orchestrator for Moodle sync — pulls assignments and quizzes into SmartStudy as tasks.
public class MoodleSyncService
{
    private readonly DBservices _dal = new DBservices();
    private readonly MoodleApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<MoodleSyncService> _logger;

    private readonly decimal _defaultAssignmentHours;
    private readonly decimal _defaultQuizHours;

    // Injects the API client, configuration, and logger; reads default task hours.
    public MoodleSyncService(MoodleApiClient api,
        IConfiguration config, ILogger<MoodleSyncService> logger)
    {
        _api = api;
        _config = config;
        _logger = logger;

        _defaultAssignmentHours = decimal.TryParse(config["Moodle:DefaultAssignmentHours"], out var ah) ? ah : 4m;
        _defaultQuizHours = decimal.TryParse(config["Moodle:DefaultQuizHours"], out var qh) ? qh : 2m;
    }

    // Fetches the raw Moodle payloads (courses, assignments, quizzes) for diagnostics.
    public async Task<object> DebugFetchAsync(string email)
    {
        var user = _dal.GetUserByEmail(email);
        if (user == null || string.IsNullOrEmpty(user.RuppinetId) || string.IsNullOrEmpty(user.RuppinetPassword))
            return new { error = "Not connected" };

        var password = DecryptPassword(user.RuppinetPassword);
        var token = await _api.GetTokenAsync(user.RuppinetId, password);
        var siteInfo = await _api.GetSiteInfoAsync(token);
        var courses = await _api.GetUserCoursesAsync(token, siteInfo.UserId);
        var courseIds = courses.Select(c => c.Id).ToArray();
        var assignments = await _api.GetAssignmentsAsync(token, courseIds);
        var quizzes = await _api.GetQuizzesAsync(token, courseIds);

        return new
        {
            userId = siteInfo.UserId,
            courseCount = courses.Count,
            courses = courses.Select(c => new { c.Id, c.ShortName, c.FullName }),
            assignmentCount = assignments.Count,
            assignments = assignments.Select(a => new { a.Id, a.CourseId, a.Name, a.DueDate, hasDueDate = a.DueDate.HasValue }),
            quizCount = quizzes.Count,
            quizzes = quizzes.Select(q => new { q.Id, q.CourseId, q.Name, q.TimeClose, hasTimeClose = q.TimeClose.HasValue })
        };
    }

    // Logs into Moodle, fetches courses/assignments/quizzes, persists tasks, and re-runs scheduling.
    public async Task<MoodleSyncResultDto> SyncAllAsync(string email)
    {
        var result = new MoodleSyncResultDto();
        var user = _dal.GetUserByEmail(email);
        if (user == null || string.IsNullOrEmpty(user.RuppinetId) || string.IsNullOrEmpty(user.RuppinetPassword))
        {
            result.Message = "Ruppinet credentials not configured. Connect Ruppinet first.";
            result.ErrorCode = "NOT_CONNECTED";
            return result;
        }

        string password;
        try
        {
            password = DecryptPassword(user.RuppinetPassword);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            _logger.LogWarning(ex, "Failed to decrypt password for Moodle sync for {Email}", email);
            result.Message = "Failed to decrypt credentials. Please reconnect your Ruppinet account.";
            result.ErrorCode = "DECRYPT_FAILED";
            return result;
        }

        _logger.LogInformation("Starting Moodle sync for {Email}", email);

        // Get Moodle token
        string token;
        try
        {
            token = await _api.GetTokenAsync(user.RuppinetId, password);
        }
        catch (MoodleApiException ex)
        {
            _logger.LogWarning("Moodle auth failed for {Email}: {Message}", email, ex.Message);
            result.Message = ex.Message;
            result.ErrorCode = ex.ErrorCode;
            return result;
        }

        // Get site info for user ID
        MoodleSiteInfo siteInfo;
        try
        {
            siteInfo = await _api.GetSiteInfoAsync(token);
        }
        catch (MoodleApiException ex)
        {
            _logger.LogWarning("Moodle site info failed for {Email}: {Message}", email, ex.Message);
            result.Message = ex.Message;
            result.ErrorCode = ex.ErrorCode;
            return result;
        }

        // Get enrolled courses
        List<MoodleCourse> moodleCourses;
        try
        {
            moodleCourses = await _api.GetUserCoursesAsync(token, siteInfo.UserId);
        }
        catch (MoodleApiException ex)
        {
            _logger.LogWarning("Moodle courses fetch failed for {Email}: {Message}", email, ex.Message);
            result.Message = ex.Message;
            result.ErrorCode = ex.ErrorCode;
            return result;
        }

        if (moodleCourses.Count == 0)
        {
            result.Success = true;
            result.Message = "No courses found in Moodle.";
            result.Warnings.Add("No enrolled courses found in Moodle");
            _dal.UpdateLastMoodleSync(email, DateTime.UtcNow);
            return result;
        }

        // Fetch assignments and quizzes in parallel
        var courseIds = moodleCourses.Select(c => c.Id).ToArray();
        List<MoodleAssignment> assignments;
        List<MoodleQuiz> quizzes;

        try
        {
            var assignTask = _api.GetAssignmentsAsync(token, courseIds);
            var quizTask = _api.GetQuizzesAsync(token, courseIds);
            await Task.WhenAll(assignTask, quizTask);
            assignments = assignTask.Result;
            quizzes = quizTask.Result;
        }
        catch (MoodleApiException ex)
        {
            _logger.LogWarning("Moodle data fetch failed for {Email}: {Message}", email, ex.Message);
            result.Message = ex.Message;
            result.ErrorCode = ex.ErrorCode;
            return result;
        }

        _logger.LogInformation("Moodle fetched {Courses} courses, {Assignments} assignments, {Quizzes} quizzes for {Email}",
            moodleCourses.Count, assignments.Count, quizzes.Count, email);

        // Process items
        ProcessMoodleItems(email, moodleCourses, assignments, quizzes, result);

        // Update sync timestamp
        _dal.UpdateLastMoodleSync(email, DateTime.UtcNow);

        // Re-run scheduling engine
        try
        {
            SmartStudy.Models.StudentTask.ScheduleAll(email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scheduling after Moodle sync failed for {Email}", email);
            result.Warnings.Add("Task scheduling failed after sync");
        }

        result.Success = true;
        result.Message = $"Synced {result.TasksCreated} new tasks, {result.TasksUpdated} updated, {result.TasksSkipped} skipped";
        _logger.LogInformation("Moodle sync completed for {Email}: {Message}", email, result.Message);
        return result;
    }

    // Maps Moodle courses to SmartStudy courses and upserts assignments/quizzes as tasks.
    private void ProcessMoodleItems(string email, List<MoodleCourse> moodleCourses,
        List<MoodleAssignment> assignments, List<MoodleQuiz> quizzes, MoodleSyncResultDto result)
    {
        // Build course mapping: Moodle course ID -> SmartStudy course ID
        var courseMap = BuildCourseMap(email);
        var moodleToSmartStudy = new Dictionary<int, int>();

        foreach (var mc in moodleCourses)
        {
            var smartStudyId = MatchCourse(mc, courseMap);
            if (smartStudyId != null)
            {
                moodleToSmartStudy[mc.Id] = smartStudyId.Value;
                result.CoursesMatched++;
            }
            else
            {
                // Create new course
                var newId = CreateCourseFromMoodle(email, mc);
                if (newId != null)
                {
                    moodleToSmartStudy[mc.Id] = newId.Value;
                    result.CoursesCreated++;
                    // Refresh course map
                    courseMap = BuildCourseMap(email);
                }
            }
        }

        var cutoffDate = DateTime.Now.AddDays(-7);

        // Process assignments
        foreach (var assignment in assignments)
        {
            if (!moodleToSmartStudy.TryGetValue(assignment.CourseId, out var courseId))
                continue;

            // Skip items with no due date or old items
            if (!assignment.DueDate.HasValue || assignment.DueDate.Value < cutoffDate)
            {
                result.TasksSkipped++;
                continue;
            }

            var moodleId = $"assign_{assignment.Id}";
            try
            {
                var existing = _dal.FindTaskByMoodleId(email, moodleId);
                if (existing != null)
                {
                    // Update DueDate if changed and task not completed
                    if (!existing.IsCompleted && assignment.DueDate.HasValue &&
                        existing.DueDate != assignment.DueDate)
                    {
                        _dal.UpdateTask(existing.TaskId, dueDate: assignment.DueDate);
                        result.TasksUpdated++;
                    }
                    else
                    {
                        result.TasksSkipped++;
                    }
                }
                else
                {
                    var title = assignment.Name.Length > 200 ? assignment.Name[..200] : assignment.Name;
                    _dal.CreateTaskWithMoodleId(courseId, email, title, "Homework",
                        _defaultAssignmentHours, assignment.DueDate, "Medium", moodleId);
                    result.TasksCreated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Moodle assignment {Id} for {Email}", assignment.Id, email);
                result.Warnings.Add($"Failed to sync assignment: {assignment.Name}");
            }
        }

        // Process quizzes
        foreach (var quiz in quizzes)
        {
            if (!moodleToSmartStudy.TryGetValue(quiz.CourseId, out var courseId))
                continue;

            // Skip items with no due date or old items
            if (!quiz.TimeClose.HasValue || quiz.TimeClose.Value < cutoffDate)
            {
                result.TasksSkipped++;
                continue;
            }

            var moodleId = $"quiz_{quiz.Id}";
            try
            {
                var existing = _dal.FindTaskByMoodleId(email, moodleId);
                if (existing != null)
                {
                    if (!existing.IsCompleted && quiz.TimeClose.HasValue &&
                        existing.DueDate != quiz.TimeClose)
                    {
                        _dal.UpdateTask(existing.TaskId, dueDate: quiz.TimeClose);
                        result.TasksUpdated++;
                    }
                    else
                    {
                        result.TasksSkipped++;
                    }
                }
                else
                {
                    var title = quiz.Name.Length > 200 ? quiz.Name[..200] : quiz.Name;
                    _dal.CreateTaskWithMoodleId(courseId, email, title, "Quiz",
                        _defaultQuizHours, quiz.TimeClose, "Medium", moodleId);
                    result.TasksCreated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Moodle quiz {Id} for {Email}", quiz.Id, email);
                result.Warnings.Add($"Failed to sync quiz: {quiz.Name}");
            }
        }
    }

    // Matches a Moodle course to a SmartStudy course by code first, then fuzzy name.
    private int? MatchCourse(MoodleCourse moodleCourse, Dictionary<string, int> courseMap)
    {
        // Tier 1: Extract course code from shortname (e.g., "129485-1-2026a")
        var codeMatch = Regex.Match(moodleCourse.ShortName, @"(\d{5,7})");
        if (codeMatch.Success && int.TryParse(codeMatch.Groups[1].Value, out var courseCode))
        {
            // Check if this code matches a SmartStudy course ID directly
            foreach (var (_, id) in courseMap)
            {
                if (id == courseCode)
                    return id;
            }
        }

        // Tier 2: Fuzzy name match using both fullname and shortname
        var matchByFull = FindCourseId(moodleCourse.FullName, courseMap);
        if (matchByFull != null) return matchByFull;

        var matchByShort = FindCourseId(moodleCourse.ShortName, courseMap);
        if (matchByShort != null) return matchByShort;

        // Tier 3: No match
        return null;
    }

    // Creates a SmartStudy course from an unmatched Moodle course and enrolls the user.
    private int? CreateCourseFromMoodle(string email, MoodleCourse mc)
    {
        var name = mc.FullName.Trim();
        if (string.IsNullOrEmpty(name)) name = mc.ShortName.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var courseId = GenerateStableId(name, 300000);
        if (!_dal.CourseExists(courseId))
        {
            var truncatedName = name.Length > 200 ? name[..200] : name;
            _dal.CreateCourse(courseId, truncatedName, null, null, null, null);
        }

        if (!_dal.UserCourseExists(email, courseId))
            _dal.CreateUserCourse(email, courseId);

        return courseId;
    }

    // Reuse same patterns from RuppinetSyncService

    // Loads the user's enrolled courses keyed by normalized course name.
    private Dictionary<string, int> BuildCourseMap(string email)
    {
        var userCourses = _dal.GetUserCoursesWithName(email);
        var map = new Dictionary<string, int>();
        foreach (var (courseId, courseName) in userCourses)
        {
            var key = NormalizeName(courseName);
            map.TryAdd(key, courseId);
        }
        return map;
    }

    // Lowercases and normalizes punctuation so course names compare reliably.
    private static string NormalizeName(string name)
    {
        return name.ToLower().Trim()
            .Replace(')', '(')
            .Replace('(', '(')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"');
    }

    // Fuzzy-matches a Moodle title against the user's course map (exact, substring, then prefix).
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

    // Computes a deterministic 8-digit course ID from a name (with collision walk).
    private int GenerateStableId(string input, int offset)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLower().Trim()));
        var value = Math.Abs(BitConverter.ToInt32(hash, 0)) % 90000000 + 10000000 + offset;

        for (int i = 0; i < 100; i++)
        {
            var existing = _dal.GetCourseById(value);
            if (existing == null || existing.CourseName.ToLower().Trim() == input.ToLower().Trim())
                return value;
            value++;
        }
        return value;
    }

    // AES-256 decrypts an IV-prefixed base64 ciphertext (matches RuppinetSyncService format).
    private string DecryptPassword(string cipherText)
    {
        var key = GetEncryptionKey();
        var fullCipher = Convert.FromBase64String(cipherText);
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
    }

    // Derives the 32-byte AES key from the configured encryption secret via SHA-256.
    private byte[] GetEncryptionKey()
    {
        var configKey = _config["Ruppinet:EncryptionKey"] ?? "SmartStudyRuppinetEncKey2026!aa";
        using var sha = System.Security.Cryptography.SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(configKey));
    }
}
