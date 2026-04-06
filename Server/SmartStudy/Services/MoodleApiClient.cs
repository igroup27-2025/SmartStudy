using System.Net;
using System.Text.Json;

namespace SmartStudy.Services;

public class MoodleApiException : Exception
{
    public string ErrorCode { get; }
    public MoodleApiException(string message, string errorCode = "API_ERROR")
        : base(message) => ErrorCode = errorCode;
}

public class MoodleSiteInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string SiteName { get; set; } = "";
}

public class MoodleCourse
{
    public int Id { get; set; }
    public string ShortName { get; set; } = "";
    public string FullName { get; set; } = "";
}

public class MoodleAssignment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public string? Intro { get; set; }
}

public class MoodleQuiz
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? TimeClose { get; set; }
}

public class MoodleApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MoodleApiClient> _logger;
    private readonly string _baseUrl;
    private readonly int _maxRetries;

    public MoodleApiClient(IHttpClientFactory httpClientFactory, ILogger<MoodleApiClient> logger, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrl = config["Moodle:BaseUrl"] ?? "https://moodle.ruppin.ac.il";
        _maxRetries = int.TryParse(config["Moodle:MaxRetries"], out var r) ? r : 2;
    }

    public async Task<string> GetTokenAsync(string username, string password)
    {
        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["service"] = "moodle_mobile_app"
        });

        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/login/token.php", content);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var errorCode = root.TryGetProperty("errorcode", out var ec) ? ec.GetString() : "unknown";
            _logger.LogWarning("Moodle login failed: {Error} ({Code})", error.GetString(), errorCode);
            throw new MoodleApiException(
                error.GetString() ?? "Authentication failed",
                errorCode == "invalidlogin" ? "AUTH_FAILED" : "API_ERROR");
        }

        if (!root.TryGetProperty("token", out var tokenEl))
            throw new MoodleApiException("No token in response", "API_ERROR");

        return tokenEl.GetString()!;
    }

    public async Task<MoodleSiteInfo> GetSiteInfoAsync(string token)
    {
        var result = await CallFunctionAsync(token, "core_webservice_get_site_info");
        return new MoodleSiteInfo
        {
            UserId = result.RootElement.GetProperty("userid").GetInt32(),
            Username = result.RootElement.GetProperty("username").GetString() ?? "",
            SiteName = result.RootElement.TryGetProperty("sitename", out var sn) ? sn.GetString() ?? "" : ""
        };
    }

    public async Task<List<MoodleCourse>> GetUserCoursesAsync(string token, int userId)
    {
        var result = await CallFunctionAsync(token, "core_enrol_get_users_courses",
            new Dictionary<string, string> { ["userid"] = userId.ToString() });

        var courses = new List<MoodleCourse>();
        foreach (var el in result.RootElement.EnumerateArray())
        {
            courses.Add(new MoodleCourse
            {
                Id = el.GetProperty("id").GetInt32(),
                ShortName = el.TryGetProperty("shortname", out var sn) ? sn.GetString() ?? "" : "",
                FullName = el.TryGetProperty("fullname", out var fn) ? fn.GetString() ?? "" : ""
            });
        }
        return courses;
    }

    public async Task<List<MoodleAssignment>> GetAssignmentsAsync(string token, int[] courseIds)
    {
        var parameters = new Dictionary<string, string>();
        for (int i = 0; i < courseIds.Length; i++)
            parameters[$"courseids[{i}]"] = courseIds[i].ToString();

        var result = await CallFunctionAsync(token, "mod_assign_get_assignments", parameters);
        var assignments = new List<MoodleAssignment>();

        if (result.RootElement.TryGetProperty("courses", out var coursesArr))
        {
            foreach (var course in coursesArr.EnumerateArray())
            {
                var moodleCourseId = course.GetProperty("id").GetInt32();
                if (!course.TryGetProperty("assignments", out var assignArr)) continue;

                foreach (var a in assignArr.EnumerateArray())
                {
                    var duedate = a.TryGetProperty("duedate", out var dd) ? dd.GetInt64() : 0;
                    assignments.Add(new MoodleAssignment
                    {
                        Id = a.GetProperty("id").GetInt32(),
                        CourseId = moodleCourseId,
                        Name = a.GetProperty("name").GetString() ?? "",
                        DueDate = duedate > 0 ? UnixToDateTime(duedate) : null,
                        Intro = a.TryGetProperty("intro", out var intro) ? intro.GetString() : null
                    });
                }
            }
        }
        return assignments;
    }

    public async Task<List<MoodleQuiz>> GetQuizzesAsync(string token, int[] courseIds)
    {
        var parameters = new Dictionary<string, string>();
        for (int i = 0; i < courseIds.Length; i++)
            parameters[$"courseids[{i}]"] = courseIds[i].ToString();

        var result = await CallFunctionAsync(token, "mod_quiz_get_quizzes_by_courses", parameters);
        var quizzes = new List<MoodleQuiz>();

        if (result.RootElement.TryGetProperty("quizzes", out var quizArr))
        {
            foreach (var q in quizArr.EnumerateArray())
            {
                var timeclose = q.TryGetProperty("timeclose", out var tc) ? tc.GetInt64() : 0;
                quizzes.Add(new MoodleQuiz
                {
                    Id = q.GetProperty("id").GetInt32(),
                    CourseId = q.GetProperty("course").GetInt32(),
                    Name = q.GetProperty("name").GetString() ?? "",
                    TimeClose = timeclose > 0 ? UnixToDateTime(timeclose) : null
                });
            }
        }
        return quizzes;
    }

    private async Task<JsonDocument> CallFunctionAsync(string token, string function,
        Dictionary<string, string>? extraParams = null)
    {
        var client = _httpClientFactory.CreateClient();
        var parameters = new Dictionary<string, string>
        {
            ["wstoken"] = token,
            ["wsfunction"] = function,
            ["moodlewsrestformat"] = "json"
        };
        if (extraParams != null)
            foreach (var kv in extraParams)
                parameters[kv.Key] = kv.Value;

        var content = new FormUrlEncodedContent(parameters);
        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/webservice/rest/server.php", content);

        var json = await response.Content.ReadAsStringAsync();

        // Check for Moodle error responses (they return 200 with error JSON)
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("exception", out _))
            {
                var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                var code = doc.RootElement.TryGetProperty("errorcode", out var ec) ? ec.GetString() : "API_ERROR";
                _logger.LogWarning("Moodle API error for {Function}: {Message} ({Code})", function, msg, code);

                if (code == "invalidtoken")
                    throw new MoodleApiException("Token expired or invalid", "TOKEN_EXPIRED");

                throw new MoodleApiException(msg ?? "Moodle API error", "API_ERROR");
            }
            return doc;
        }
        catch (JsonException)
        {
            throw new MoodleApiException($"Invalid JSON from Moodle for {function}", "API_ERROR");
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpClient client,
        HttpMethod method, string url, HttpContent content)
    {
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(method, url) { Content = content };
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode || attempt == _maxRetries)
                    return response;

                var status = (int)response.StatusCode;
                if (status is >= 500 or 408 or 429)
                {
                    _logger.LogWarning("Moodle API returned {StatusCode}, retry {Attempt}/{Max}",
                        status, attempt + 1, _maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "Moodle HTTP error, retry {Attempt}/{Max}", attempt + 1, _maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        throw new MoodleApiException("Moodle server is unavailable after retries", "SERVICE_UNAVAILABLE");
    }

    private static DateTime UnixToDateTime(long unixTimestamp)
        => DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).LocalDateTime;
}
