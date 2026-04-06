using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/schedule")]
[Authorize]
public class ScheduleImportController : ControllerBase
{
    private readonly ScheduleImportService _importService;
    private readonly DBservices _dal;

    public ScheduleImportController(ScheduleImportService importService, DBservices dal)
    {
        _importService = importService;
        _dal = dal;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpPost("import")]
    public async Task<IActionResult> ImportSchedule(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "File too large (max 10MB)" });

        var email = GetEmail();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        using var stream = file.OpenReadStream();

        switch (ext)
        {
            case ".pdf":
                var pdfResult = await _importService.ImportScheduleAsync(stream, email);
                return Ok(pdfResult);

            case ".csv":
                var csvResult = await ImportFromCsvAsync(stream, email);
                return Ok(csvResult);

            case ".xlsx":
                var excelResult = await ImportFromExcelAsync(stream, email);
                return Ok(excelResult);

            case ".json":
                var jsonResult = await ImportFromJsonAsync(stream, email);
                return Ok(jsonResult);

            default:
                return BadRequest(new { message = "Unsupported file format. Use .pdf, .csv, .xlsx, or .json" });
        }
    }

    private async Task<ScheduleImportResultDto> ImportFromCsvAsync(Stream stream, string email)
    {
        var result = new ScheduleImportResultDto();
        using var reader = new StreamReader(stream);
        var header = await reader.ReadLineAsync();
        if (header == null) return result;

        var cols = header.Split(',').Select(c => c.Trim().ToLower()).ToList();

        while (await reader.ReadLineAsync() is { } line)
        {
            var values = ParseCsvLine(line);
            if (values.Count < cols.Count) continue;

            try
            {
                var entry = new ImportEntry
                {
                    CourseName = GetCol(cols, values, "coursename"),
                    CourseCode = GetCol(cols, values, "coursecode"),
                    Date = GetCol(cols, values, "date"),
                    StartTime = GetCol(cols, values, "starttime"),
                    EndTime = GetCol(cols, values, "endtime"),
                    Location = GetCol(cols, values, "location"),
                    InstructorName = GetCol(cols, values, "instructorname")
                };
                await SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row error: {ex.Message}");
            }
        }
        return result;
    }

    private async Task<ScheduleImportResultDto> ImportFromExcelAsync(Stream stream, string email)
    {
        var result = new ScheduleImportResultDto();
        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

        using var package = new OfficeOpenXml.ExcelPackage(stream);
        var ws = package.Workbook.Worksheets.FirstOrDefault();
        if (ws == null) return result;

        var rowCount = ws.Dimension?.Rows ?? 0;
        var colCount = ws.Dimension?.Columns ?? 0;
        if (rowCount < 2 || colCount < 3) return result;

        var cols = new List<string>();
        for (int c = 1; c <= colCount; c++)
            cols.Add((ws.Cells[1, c].Text ?? "").Trim().ToLower());

        for (int r = 2; r <= rowCount; r++)
        {
            try
            {
                var values = new List<string>();
                for (int c = 1; c <= colCount; c++)
                    values.Add(ws.Cells[r, c].Text ?? "");

                var entry = new ImportEntry
                {
                    CourseName = GetCol(cols, values, "coursename"),
                    CourseCode = GetCol(cols, values, "coursecode"),
                    Date = GetCol(cols, values, "date"),
                    StartTime = GetCol(cols, values, "starttime"),
                    EndTime = GetCol(cols, values, "endtime"),
                    Location = GetCol(cols, values, "location"),
                    InstructorName = GetCol(cols, values, "instructorname")
                };
                await SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row {r} error: {ex.Message}");
            }
        }
        return result;
    }

    private async Task<ScheduleImportResultDto> ImportFromJsonAsync(Stream stream, string email)
    {
        var result = new ScheduleImportResultDto();
        var entries = await JsonSerializer.DeserializeAsync<List<ImportEntry>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (entries == null) return result;

        foreach (var entry in entries)
        {
            try
            {
                await SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Entry error: {ex.Message}");
            }
        }
        return result;
    }

    private async Task SaveImportEntry(ImportEntry entry, string email, ScheduleImportResultDto result)
    {
        if (string.IsNullOrEmpty(entry.CourseName) && string.IsNullOrEmpty(entry.CourseCode)) return;

        var courseName = entry.CourseName ?? entry.CourseCode ?? "Unknown";
        var courseCode = entry.CourseCode ?? "";

        // Parse or generate course ID
        int courseId;
        if (!string.IsNullOrEmpty(courseCode) && courseCode.Contains('-'))
        {
            var parts = courseCode.Split('-');
            courseId = int.Parse(parts[0]) * 100 + (parts.Length > 1 ? int.Parse(parts[1]) : 0);
        }
        else
        {
            courseId = Math.Abs(courseName.GetHashCode()) % 100000000;
        }

        // Find or create course
        var course = await _dal.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            await _dal.CreateCourseAsync(courseId, courseName, null, null, GetCurrentSemester(), null);
            result.CoursesCreated++;
        }

        // Instructor
        if (!string.IsNullOrEmpty(entry.InstructorName))
        {
            var instructor = await _dal.FindInstructorByNameAsync(entry.InstructorName);
            if (instructor == null)
            {
                var instructorId = await _dal.CreateInstructorAsync(entry.InstructorName);
                await _dal.UpdateCourseInstructorAsync(courseId, instructorId);
            }
            else
            {
                await _dal.UpdateCourseInstructorAsync(courseId, instructor.InstructorId);
            }
        }

        // Enroll
        if (!await _dal.UserCourseExistsAsync(email, courseId))
        {
            await _dal.CreateUserCourseAsync(email, courseId);
        }

        // Parse date and times
        if (!DateTime.TryParse(entry.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return;
        if (!TimeSpan.TryParse(entry.StartTime, out var startTime)) return;
        if (!TimeSpan.TryParse(entry.EndTime, out var endTime)) return;

        var from = date.Date + startTime;
        var to = date.Date + endTime;

        // Check for duplicate
        if (await _dal.ClassEventExistsAsync(email, courseId, from, to))
        {
            result.EntriesSkipped++;
            return;
        }

        await _dal.CreateClassEventAsync(email, from, to, true, null, courseId, entry.Location,
            (decimal)(to - from).TotalHours);
        result.EventsCreated++;

        result.Courses ??= new List<ImportedCourseDto>();
        var existing = result.Courses.FirstOrDefault(c => c.CourseId == courseId);
        if (existing != null) existing.EventCount++;
        else result.Courses.Add(new ImportedCourseDto { CourseId = courseId, CourseName = courseName, EventCount = 1 });
    }

    private static string GetCol(List<string> headers, List<string> values, string name)
    {
        var idx = headers.IndexOf(name);
        return idx >= 0 && idx < values.Count ? values[idx].Trim() : "";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = "";
        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { result.Add(current); current = ""; continue; }
            current += c;
        }
        result.Add(current);
        return result;
    }

    private static string GetCurrentSemester()
    {
        var now = DateTime.Now;
        var suffix = now.Month >= 10 || now.Month <= 2 ? "A" : "B";
        var year = now.Month >= 10 ? now.Year : now.Year - 1;
        return $"{year}{suffix}";
    }

    private class ImportEntry
    {
        public string? CourseName { get; set; }
        public string? CourseCode { get; set; }
        public string? Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Location { get; set; }
        public string? InstructorName { get; set; }
    }
}
