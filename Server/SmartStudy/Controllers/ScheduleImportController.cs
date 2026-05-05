using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for importing course schedules from PDF, CSV, XLSX, or JSON files.
[ApiController]
[Route("api/schedule")]
[Authorize]
public class ScheduleImportController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Routes the uploaded schedule file to the appropriate parser by extension.
    [HttpPost("import")]
    public IActionResult ImportSchedule(IFormFile file)
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
                var pdfResult = Course.ImportSchedule(stream, email);
                return Ok(pdfResult);

            case ".csv":
                var csvResult = ImportFromCsv(stream, email);
                return Ok(csvResult);

            case ".xlsx":
                var excelResult = ImportFromExcel(stream, email);
                return Ok(excelResult);

            case ".json":
                var jsonResult = ImportFromJson(stream, email);
                return Ok(jsonResult);

            default:
                return BadRequest(new { message = "Unsupported file format. Use .pdf, .csv, .xlsx, or .json" });
        }
    }

    // Parses a CSV schedule file and persists each row as a course/class event.
    private ScheduleImportResultDto ImportFromCsv(Stream stream, string email)
    {
        var result = new ScheduleImportResultDto();
        using var reader = new StreamReader(stream);
        var header = reader.ReadLine();
        if (header == null) return result;

        var cols = header.Split(',').Select(c => c.Trim().ToLower()).ToList();

        string? line;
        while ((line = reader.ReadLine()) != null)
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
                SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row error: {ex.Message}");
            }
        }
        return result;
    }

    // Parses an Excel schedule file via EPPlus and persists each row.
    private ScheduleImportResultDto ImportFromExcel(Stream stream, string email)
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
                SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row {r} error: {ex.Message}");
            }
        }
        return result;
    }

    // Parses a JSON schedule file as a list of import entries and persists each.
    private ScheduleImportResultDto ImportFromJson(Stream stream, string email)
    {
        var result = new ScheduleImportResultDto();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var entries = JsonSerializer.Deserialize<List<ImportEntry>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (entries == null) return result;

        foreach (var entry in entries)
        {
            try
            {
                SaveImportEntry(entry, email, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Entry error: {ex.Message}");
            }
        }
        return result;
    }

    // Upserts a course/instructor/enrollment and creates the matching weekly class event.
    private void SaveImportEntry(ImportEntry entry, string email, ScheduleImportResultDto result)
    {
        if (string.IsNullOrEmpty(entry.CourseName) && string.IsNullOrEmpty(entry.CourseCode)) return;

        var courseName = entry.CourseName ?? entry.CourseCode ?? "Unknown";
        var courseCode = entry.CourseCode ?? "";

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

        var course = Course.GetById(courseId);
        if (course == null)
        {
            Course.Create(courseId, courseName, null, null, GetCurrentSemester(), null);
            result.CoursesCreated++;
        }

        if (!string.IsNullOrEmpty(entry.InstructorName))
        {
            var instructor = Course.FindInstructorByName(entry.InstructorName);
            if (instructor == null)
            {
                var instructorId = Course.CreateInstructor(entry.InstructorName);
                Course.UpdateCourseInstructor(courseId, instructorId);
            }
            else
            {
                Course.UpdateCourseInstructor(courseId, instructor.InstructorId);
            }
        }

        if (!Course.UserCourseExists(email, courseId))
        {
            Course.CreateUserCourse(email, courseId);
        }

        if (!DateTime.TryParse(entry.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return;
        if (!TimeSpan.TryParse(entry.StartTime, out var startTime)) return;
        if (!TimeSpan.TryParse(entry.EndTime, out var endTime)) return;

        var from = date.Date + startTime;
        var to = date.Date + endTime;

        if (Course.ClassEventExists(email, courseId, from, to))
        {
            result.EntriesSkipped++;
            return;
        }

        Course.CreateClassEvent(email, from, to, true, null, courseId, entry.Location,
            (decimal)(to - from).TotalHours);
        result.EventsCreated++;

        result.Courses ??= new List<ImportedCourseDto>();
        var existing = result.Courses.FirstOrDefault(c => c.CourseId == courseId);
        if (existing != null) existing.EventCount++;
        else result.Courses.Add(new ImportedCourseDto { CourseId = courseId, CourseName = courseName, EventCount = 1 });
    }

    // Looks up a column value by header name, returning empty string if missing.
    private static string GetCol(List<string> headers, List<string> values, string name)
    {
        var idx = headers.IndexOf(name);
        return idx >= 0 && idx < values.Count ? values[idx].Trim() : "";
    }

    // Splits a CSV row into fields, handling quoted commas.
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

    // Computes the current academic semester code (e.g. "2025A" or "2025B").
    private static string GetCurrentSemester()
    {
        var now = DateTime.Now;
        var suffix = now.Month >= 10 || now.Month <= 2 ? "A" : "B";
        var year = now.Month >= 10 ? now.Year : now.Year - 1;
        return $"{year}{suffix}";
    }

    // Internal row shape produced by the CSV/Excel/JSON parsers.
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
