using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SmartStudy.Services;

public class RuppinetApiException : Exception
{
    public string ErrorCode { get; }
    public RuppinetApiException(string message, string errorCode = "API_ERROR")
        : base(message) => ErrorCode = errorCode;
}

public class RuppinetApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RuppinetApiClient> _logger;
    private readonly string _baseUrl;
    private readonly int _maxRetries;

    public RuppinetApiClient(IHttpClientFactory httpClientFactory, ILogger<RuppinetApiClient> logger, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrl = config["Ruppinet:BaseUrl"] ?? "https://ruppinet.ruppin.ac.il/Portals/api";
        _maxRetries = int.TryParse(config["Ruppinet:MaxRetries"], out var r) ? r : 2;
    }

    public async Task<string?> LoginAsync(string zht, string password)
    {
        var client = _httpClientFactory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            loginType = "student",
            zht,
            password,
            captchaToken = (string?)null
        });

        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/Login/Login", body);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ruppinet login failed with status {StatusCode}", response.StatusCode);
            throw new RuppinetApiException(
                $"Ruppinet login failed (HTTP {(int)response.StatusCode})", "AUTH_FAILED");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // CAPTCHA detection
        if (root.TryGetProperty("captchaRequired", out var captcha) && captcha.GetBoolean())
        {
            _logger.LogWarning("Ruppinet requires CAPTCHA for user {Zht}", zht);
            throw new RuppinetApiException(
                "Ruppinet requires CAPTCHA verification. Please log in manually at ruppinet.ruppin.ac.il first, then retry.",
                "CAPTCHA_REQUIRED");
        }

        if (root.TryGetProperty("success", out var success) && success.GetBoolean()
            && root.TryGetProperty("token", out var token))
        {
            return token.GetString();
        }
        return null;
    }

    public async Task<List<RuppinetScheduleEvent>> GetScheduleAsync(string token, DateTime from, DateTime to)
    {
        var client = CreateAuthClient(token);
        var body = JsonSerializer.Serialize(new
        {
            fromDate = from.ToString("yyyy-MM-ddT00:00:00"),
            toDate = to.ToString("yyyy-MM-ddT00:00:00")
        });

        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/Home/ScheduleData", body);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ruppinet schedule fetch failed with status {StatusCode}", response.StatusCode);
            throw new RuppinetApiException(
                $"Failed to fetch schedule (HTTP {(int)response.StatusCode})");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var events = new List<RuppinetScheduleEvent>();
        _logger.LogDebug("Schedule raw response length: {Length}", json.Length);
        _logger.LogDebug("Schedule top-level keys: {Keys}",
            string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name)));

        if (!doc.RootElement.TryGetProperty("events", out var eventsArr))
        {
            _logger.LogDebug("No 'events' key found in schedule response");
            return events;
        }
        _logger.LogDebug("Schedule events count: {Count}", eventsArr.GetArrayLength());

        foreach (var evt in eventsArr.EnumerateArray())
        {
            var dateStr = evt.GetProperty("date").GetString();
            if (!DateTime.TryParse(dateStr, out var date)) continue;

            var data = evt.GetProperty("data");
            var title = data.GetProperty("title").GetString() ?? "";
            var moreInfo = data.TryGetProperty("moreInfo", out var mi) ? mi.GetString() : null;
            var place = data.TryGetProperty("place", out var pl) ? pl.GetString() : null;

            var startTimeStr = data.TryGetProperty("startTime", out var st) ? st.GetString() : null;
            var endTimeStr = data.TryGetProperty("endTime", out var et) ? et.GetString() : null;

            if (string.IsNullOrEmpty(moreInfo) && string.IsNullOrEmpty(place))
                continue; // holidays/special days have no instructor or room

            var startTime = ParseTimeFromRuppinet(startTimeStr);
            var endTime = ParseTimeFromRuppinet(endTimeStr);

            events.Add(new RuppinetScheduleEvent
            {
                Date = date.Date,
                Title = title,
                Instructor = moreInfo,
                Location = place,
                StartTime = startTime,
                EndTime = endTime
            });
        }
        return events;
    }

    public async Task<List<RuppinetExam>> GetExamsAsync(string token)
    {
        var client = CreateAuthClient(token);
        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/StudentExams/Data", "null");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ruppinet exams fetch failed with status {StatusCode}", response.StatusCode);
            throw new RuppinetApiException(
                $"Failed to fetch exams (HTTP {(int)response.StatusCode})");
        }

        var json = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("Exams raw response length: {Length}", json.Length);
        using var doc = JsonDocument.Parse(json);

        var exams = new List<RuppinetExam>();

        // Try multiple JSON structures
        JsonElement clientData;
        if (doc.RootElement.TryGetProperty("collapsedExams", out var collapsed)
            && collapsed.TryGetProperty("clientData", out clientData))
        {
            // structure: { collapsedExams: { clientData: [...] } }
        }
        else if (doc.RootElement.TryGetProperty("clientData", out clientData))
        {
            // structure: { clientData: [...] }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            clientData = doc.RootElement;
        }
        else
        {
            _logger.LogDebug("Exams top-level keys: {Keys}",
                string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name)));
            return exams;
        }

        var totalItems = 0;
        var skippedExpired = 0;
        foreach (var item in clientData.EnumerateArray())
        {
            totalItems++;
            var expired = item.TryGetProperty("expired", out var exp) ? exp.GetInt32() : 1;

            if (totalItems <= 5 || expired == 0)
            {
                var examName = item.TryGetProperty("krs_shm", out var n) ? n.GetString() : "?";
                var examType = item.TryGetProperty("krs_bhn_shm", out var t) ? t.GetString() : "?";
                var moedKrs = item.TryGetProperty("krs_bhn_moed_krs", out var mk) ? mk.ToString() : "?";
                var dateVal = item.TryGetProperty("date", out var dv) ? dv.GetString() : "?";
                _logger.LogDebug("Exam #{Num}: name={Name}, type={Type}, nmrtr={Nmrtr}, date={Date}, expired={Expired}",
                    totalItems, examName, examType, moedKrs, dateVal, expired);
            }

            if (expired != 0)
            {
                skippedExpired++;
                continue;
            }

            var dateStr = item.TryGetProperty("date", out var d) ? d.GetString() : null;
            if (!DateTime.TryParse(dateStr, out var date)) continue;

            var timeRange = item.TryGetProperty("krs_bhn_moed_mishaa", out var tr) ? tr.GetString() : "";
            var (startTime, durationHours, durationEstimated) = ParseExamTimeRange(timeRange);

            exams.Add(new RuppinetExam
            {
                CourseCode = item.TryGetProperty("krs_mis_kvuza", out var cc) ? cc.GetString() ?? "" : "",
                CourseName = item.TryGetProperty("krs_shm", out var cn) ? cn.GetString() ?? "" : "",
                CourseNmrtr = item.TryGetProperty("krs_bhn_moed_krs", out var nmrtr) ? nmrtr.GetInt32() : 0,
                ExamType = item.TryGetProperty("krs_bhn_shm", out var et) ? et.GetString() ?? "" : "",
                MoedMis = item.TryGetProperty("krs_bhn_moed_mis", out var mis) ? mis.GetInt32() : 1,
                Date = date,
                StartTime = startTime,
                DurationHours = durationHours,
                DurationEstimated = durationEstimated,
                Instructor = item.TryGetProperty("pm_shm", out var pm) ? pm.GetString() : null,
                Location = item.TryGetProperty("hdr_shm", out var hdr) ? hdr.GetString() : null,
                Semester = item.TryGetProperty("krs_snl", out var snl) ? snl.GetString() : null
            });
        }
        _logger.LogDebug("Exams total={Total}, skippedExpired={Skipped}, parsed={Parsed}",
            totalItems, skippedExpired, exams.Count);
        return exams;
    }

    public async Task<List<RuppinetCourse>> GetCoursesAsync(string token)
    {
        var client = CreateAuthClient(token);
        var response = await SendWithRetryAsync(client, HttpMethod.Post,
            $"{_baseUrl}/StudentCourses/Data", "null");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ruppinet courses fetch failed with status {StatusCode}", response.StatusCode);
            throw new RuppinetApiException(
                $"Failed to fetch courses (HTTP {(int)response.StatusCode})");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var courses = new List<RuppinetCourse>();
        if (!doc.RootElement.TryGetProperty("courses", out var coursesObj)) return courses;
        if (!coursesObj.TryGetProperty("clientData", out var clientData)) return courses;

        foreach (var item in clientData.EnumerateArray())
        {
            var semester = item.TryGetProperty("krs_snl", out var snl) ? snl.GetString() : null;
            var currentSemester = GetCurrentHebrewSemester();
            if (semester != currentSemester) continue;

            courses.Add(new RuppinetCourse
            {
                Nmrtr = item.TryGetProperty("krs_nmrtr", out var n) ? n.GetInt32() : 0,
                Code = item.TryGetProperty("krs_mis_kvuza", out var c) ? c.GetString() ?? "" : "",
                Name = item.TryGetProperty("krs_shm", out var nm) ? nm.GetString() ?? "" : "",
                EnglishName = item.TryGetProperty("krs_engshm", out var en) ? en.GetString() : null,
                Credits = item.TryGetProperty("dispzikui", out var cr) ? cr.GetDecimal() : 0,
                WeeklyHours = item.TryGetProperty("dispshaot", out var wh) ? wh.GetDecimal() : 0,
                Instructors = item.TryGetProperty("all_pms_shm", out var inst) ? inst.GetString() : null,
                SemesterCode = item.TryGetProperty("krs_sms", out var sc) ? sc.GetString() : null,
                Semester = semester
            });
        }
        return courses;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client, HttpMethod method, string url, string body)
    {
        HttpResponseMessage? response = null;
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            var request = new HttpRequestMessage(method, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return response;

            var status = (int)response.StatusCode;
            var isTransient = status >= 500 || status == 408 || status == 429;
            if (!isTransient || attempt >= _maxRetries)
                break;

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 1s, 2s
            _logger.LogWarning("Ruppinet request to {Url} returned {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{Max})",
                url, status, delay.TotalSeconds, attempt + 1, _maxRetries);
            await Task.Delay(delay);
        }
        return response!;
    }

    private HttpClient CreateAuthClient(string token)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static TimeSpan ParseTimeFromRuppinet(string? timeStr)
    {
        if (string.IsNullOrEmpty(timeStr)) return TimeSpan.Zero;
        if (DateTime.TryParse(timeStr, out var dt))
            return dt.TimeOfDay;
        return TimeSpan.Zero;
    }

    private static (TimeSpan startTime, int durationHours, bool estimated) ParseExamTimeRange(string? range)
    {
        if (string.IsNullOrEmpty(range)) return (TimeSpan.Zero, 3, true);
        var parts = range.Split('-');
        if (parts.Length < 2) return (TimeSpan.Zero, 3, true);

        TimeSpan.TryParse(parts[0].Trim(), out var start);
        TimeSpan.TryParse(parts[1].Trim(), out var end);
        var duration = (int)Math.Ceiling((end - start).TotalHours);
        if (duration <= 0) return (start, 3, true);
        return (start, duration, false);
    }

    internal static string GetCurrentHebrewSemester()
    {
        var now = DateTime.Now;
        // Determine which academic year we're in:
        // Oct-Dec → academic year starts this year; Jan-Sep → started last year
        var academicYear = now.Month >= 10 ? now.Year : now.Year - 1;

        // Map academic year to Hebrew year suffix
        // Academic 2020-2021 = תשפא, 2021-2022 = תשפב, ..., 2029-2030 = תשצ
        var offset = (academicYear - 2020) % 10;
        var suffix = offset switch
        {
            0 => "תשפא",
            1 => "תשפב",
            2 => "תשפג",
            3 => "תשפד",
            4 => "תשפה",
            5 => "תשפו",
            6 => "תשפז",
            7 => "תשפח",
            8 => "תשפט",
            9 => "תשצ",
            _ => "תשפו"
        };
        return suffix;
    }
}

public class RuppinetScheduleEvent
{
    public DateTime Date { get; set; }
    public string Title { get; set; } = "";
    public string? Instructor { get; set; }
    public string? Location { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class RuppinetExam
{
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int CourseNmrtr { get; set; }
    public string ExamType { get; set; } = "";
    public int MoedMis { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationHours { get; set; }
    public bool DurationEstimated { get; set; }
    public string? Instructor { get; set; }
    public string? Location { get; set; }
    public string? Semester { get; set; }
}

public class RuppinetCourse
{
    public int Nmrtr { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? EnglishName { get; set; }
    public decimal Credits { get; set; }
    public decimal WeeklyHours { get; set; }
    public string? Instructors { get; set; }
    public string? SemesterCode { get; set; }
    public string? Semester { get; set; }
}
