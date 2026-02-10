using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;
using UglyToad.PdfPig;

namespace SmartStudy.Services;

public class ScheduleImportService
{
    private readonly SmartStudyDbContext _db;

    public ScheduleImportService(SmartStudyDbContext db) => _db = db;

    // ── Public API ──────────────────────────────────────────────

    public async Task<ScheduleImportResultDto> ImportScheduleAsync(Stream pdfStream, string email)
    {
        var raw = ParsePdf(pdfStream);

        // Filter: skip entries where hours = 0
        var filtered = raw.Where(e => e.Hours > 0).ToList();
        var skipped = raw.Count - filtered.Count;

        // Merge duplicates: same course code + same date → keep widest time range
        var entries = MergeDuplicates(filtered);

        // Save to database
        var result = await SaveToDatabaseAsync(entries, email);
        result.EntriesSkipped += skipped;
        return result;
    }

    // ── PDF Parsing ─────────────────────────────────────────────

    private List<RawEntry> ParsePdf(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var allEntries = new List<RawEntry>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            var pageHeight = page.Height;

            // Group words into horizontal bands (rows) by Y position
            var rows = GroupIntoRows(words, pageHeight);

            // Parse entries from the rows
            allEntries.AddRange(ParseRows(rows));
        }

        return allEntries;
    }

    private List<List<PdfWord>> GroupIntoRows(List<UglyToad.PdfPig.Content.Word> words, double pageHeight)
    {
        // Convert to our PdfWord with top-down Y, fix RTL character order
        var pdfWords = words.Select(w => new PdfWord
        {
            Text = FixRtlWord(w.Text),
            X = w.BoundingBox.Left,
            Y = pageHeight - w.BoundingBox.Top, // Convert to top-down
            Width = w.BoundingBox.Width,
            Height = w.BoundingBox.Height
        }).OrderBy(w => w.Y).ToList();

        // Group into rows with Y tolerance
        var rows = new List<List<PdfWord>>();
        var currentRow = new List<PdfWord>();
        double currentY = -100;
        const double yTolerance = 6.0;

        foreach (var word in pdfWords)
        {
            if (currentRow.Count == 0 || Math.Abs(word.Y - currentY) <= yTolerance)
            {
                currentRow.Add(word);
                if (currentRow.Count == 1) currentY = word.Y;
            }
            else
            {
                rows.Add(currentRow.OrderBy(w => w.X).ToList());
                currentRow = new List<PdfWord> { word };
                currentY = word.Y;
            }
        }
        if (currentRow.Count > 0)
            rows.Add(currentRow.OrderBy(w => w.X).ToList());

        return rows;
    }

    private List<RawEntry> ParseRows(List<List<PdfWord>> rows)
    {
        // Phase 1: Group rows into entries by date boundaries
        // Keep rows separate to preserve per-row RTL ordering
        var entryRowGroups = new List<List<List<PdfWord>>>();
        List<List<PdfWord>>? currentGroup = null;

        foreach (var row in rows)
        {
            var rowText = string.Join(" ", row.Select(w => w.Text));
            var hasDate = Regex.IsMatch(rowText, @"\d{2}/\d{2}/\d{4}");

            if (hasDate)
            {
                if (currentGroup != null && currentGroup.Count > 0)
                    entryRowGroups.Add(currentGroup);
                currentGroup = new List<List<PdfWord>> { row };
            }
            else if (currentGroup != null)
            {
                currentGroup.Add(row);
            }
        }
        if (currentGroup != null && currentGroup.Count > 0)
            entryRowGroups.Add(currentGroup);

        // Phase 2: Parse each row group into an entry
        var entries = new List<RawEntry>();
        foreach (var rowGroup in entryRowGroups)
        {
            var entry = new RawEntry();
            entry.Rows = rowGroup;
            // Flatten for pattern matching (order doesn't matter for regex)
            foreach (var row in rowGroup)
                entry.AllWords.AddRange(row);
            ExtractFromWords(entry);

            if (!string.IsNullOrEmpty(entry.CourseCode) && entry.Date != default)
                entries.Add(entry);
        }

        return entries;
    }

    private void ExtractFromWords(RawEntry entry)
    {
        var allText = string.Join(" ", entry.AllWords.Select(w => w.Text));

        // Course code: NNNNNN-NN
        var codeMatch = Regex.Match(allText, @"(\d{6})-(\d{1,2})");
        if (codeMatch.Success)
            entry.CourseCode = codeMatch.Value;

        // Date: DD/MM/YYYY
        var dateMatch = Regex.Match(allText, @"(\d{2})/(\d{2})/(\d{4})");
        if (dateMatch.Success)
        {
            if (DateTime.TryParseExact(dateMatch.Value, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                entry.Date = date;
        }

        // Times: HH:MM (collect all, expect 2)
        var timeMatches = Regex.Matches(allText, @"(\d{1,2}):(\d{2})");
        var times = new List<TimeSpan>();
        foreach (Match tm in timeMatches)
        {
            var h = int.Parse(tm.Groups[1].Value);
            var m = int.Parse(tm.Groups[2].Value);
            if (h >= 0 && h <= 23 && m >= 0 && m <= 59)
                times.Add(new TimeSpan(h, m, 0));
        }
        // Assign from/to (from = earlier, to = later)
        if (times.Count >= 2)
        {
            var sorted = times.Distinct().OrderBy(t => t).ToList();
            entry.StartTime = sorted.First();
            entry.EndTime = sorted.Last();
        }
        else if (times.Count == 1)
        {
            entry.StartTime = times[0];
            entry.EndTime = times[0].Add(TimeSpan.FromMinutes(1));
        }

        // Hours: N.NN (decimal)
        var hoursMatches = Regex.Matches(allText, @"(\d+)\.(\d{2})");
        foreach (Match hm in hoursMatches)
        {
            if (decimal.TryParse(hm.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var hrs) && hrs < 100)
            {
                entry.Hours = hrs;
                break;
            }
        }

        // Extract course name, instructor, and room from remaining text
        ExtractTextFields(entry);
    }

    private void ExtractTextFields(RawEntry entry)
    {
        // Collect text words (non-pattern) and reverse for RTL reading order
        var textWords = new List<PdfWord>();
        foreach (var word in entry.AllWords)
        {
            var t = word.Text.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            // Skip if it's a pattern we already extracted
            if (Regex.IsMatch(t, @"^\d{6}-\d{1,2}$")) continue;
            if (Regex.IsMatch(t, @"^\d{2}/\d{2}/\d{4}$")) continue;
            if (Regex.IsMatch(t, @"^\d{1,2}:\d{2}$")) continue;
            if (Regex.IsMatch(t, @"^\d+\.\d{2}$")) continue;
            if (Regex.IsMatch(t, @"^\d{1,2}$")) continue; // Row numbers, single digits
            // Skip Hebrew date parts
            if (IsHebrewDatePart(t)) continue;
            // Skip day-of-week single Hebrew letters
            if (t.Length == 1 && "אבגדהוש".Contains(t[0])) continue;
            // Skip Hebrew "total" line
            if (t.Contains("סה\"כ") || t == "שעות" || t == ":") continue;

            textWords.Add(word);
        }

        // Build text in RTL reading order (reverse X-sorted words)
        var fullText = BuildHebrewText(textWords);

        // Extract instructor: starts with title prefix
        var instructorPattern = @"(מר|גב'|ד""ר|פרופ'?)\s+[\u0590-\u05FF\s,'-]+";
        var instrMatch = Regex.Match(fullText, instructorPattern);
        if (instrMatch.Success)
        {
            entry.InstructorName = instrMatch.Value.Trim().TrimEnd(',').Trim();
            fullText = fullText.Replace(instrMatch.Value, " ").Trim();
        }

        // Extract room: contains building/room keywords or building code patterns
        var roomPattern = @"(בנין|בניין|חדר|קומה|מעב)[\u0590-\u05FF0-9\s\-\.]+";
        var roomMatch = Regex.Match(fullText, roomPattern);
        if (roomMatch.Success)
        {
            entry.Location = roomMatch.Value.Trim();
            fullText = fullText.Replace(roomMatch.Value, " ").Trim();
        }
        // Also clean up stray room numbers like "113", "-014"
        fullText = Regex.Replace(fullText, @"\s*-?\d{3,4}\s*", " ").Trim();

        // Clean up remaining text = course name
        fullText = Regex.Replace(fullText, @"\s+", " ").Trim();
        // Remove stray punctuation and dashes
        fullText = fullText.Trim('-', ' ');
        if (!string.IsNullOrWhiteSpace(fullText))
            entry.CourseName = fullText;
    }

    private static bool IsHebrewDatePart(string text)
    {
        // Hebrew date components: month names, day ordinals, year prefix
        var hebrewDateWords = new HashSet<string>
        {
            "תשפ\"ו", "תשפ\"ז", "תשפ\"ח", "תשפ\"ד", "תשפ\"ה",
            "טבת", "שבט", "אדר", "ניסן", "אייר", "סיוון", "תמוז", "אב", "אלול",
            "תשרי", "חשוון", "כסלו",
            "טו'", "טז'", "יז'", "יח'", "יט'", "כ'", "כא'", "כב'", "כג'", "כד'", "כה'", "כו'", "כז'", "כח'", "כט'", "ל'",
            "א'", "ב'", "ג'", "ד'", "ה'", "ו'", "ז'", "ח'", "ט'", "י'", "יא'", "יב'", "יג'", "יד'"
        };
        return hebrewDateWords.Contains(text) || text.StartsWith("תש");
    }

    // ── Merge Duplicates ────────────────────────────────────────

    private List<RawEntry> MergeDuplicates(List<RawEntry> entries)
    {
        return entries
            .GroupBy(e => (e.CourseCode, e.Date.Date))
            .Select(g => g.OrderByDescending(e => e.EndTime - e.StartTime).First())
            .ToList();
    }

    // ── Database Operations ─────────────────────────────────────

    private async Task<ScheduleImportResultDto> SaveToDatabaseAsync(List<RawEntry> entries, string email)
    {
        var result = new ScheduleImportResultDto();
        var courseMap = new Dictionary<string, (int courseId, string name, int eventCount)>();

        foreach (var entry in entries)
        {
            try
            {
                // 1. Compute CourseId from course code
                var courseId = ParseCourseId(entry.CourseCode!);
                var courseName = entry.CourseName ?? entry.CourseCode ?? "Unknown Course";

                // 2. Find or create course (update name on re-import)
                var course = await _db.Courses.FindAsync(courseId);
                if (course == null)
                {
                    course = new Course
                    {
                        CourseId = courseId,
                        CourseName = courseName,
                        WeeklyHours = entry.Hours,
                        Semester = GetCurrentSemester()
                    };
                    _db.Courses.Add(course);
                    result.CoursesCreated++;
                }
                else
                {
                    // Update course name on re-import (fixes RTL issues etc.)
                    if (!string.IsNullOrWhiteSpace(courseName) && courseName != entry.CourseCode)
                        course.CourseName = courseName;
                }

                // Create or update instructor if name is available
                if (!string.IsNullOrEmpty(entry.InstructorName))
                {
                    var instructor = await _db.Instructors
                        .FirstOrDefaultAsync(i => i.InstructorName == entry.InstructorName);
                    if (instructor == null)
                    {
                        instructor = new Instructor { InstructorName = entry.InstructorName };
                        _db.Instructors.Add(instructor);
                        await _db.SaveChangesAsync();
                    }
                    course.InstructorId = instructor.InstructorId;
                }
                await _db.SaveChangesAsync();

                // 3. Ensure user enrollment
                var enrolled = await _db.UserCourses
                    .AnyAsync(uc => uc.Email == email && uc.CourseId == courseId);
                if (!enrolled)
                {
                    _db.UserCourses.Add(new UserCourse { Email = email, CourseId = courseId });
                    await _db.SaveChangesAsync();
                }

                // 4. Check for existing event (avoid duplicates on re-import)
                var from = entry.Date.Date + entry.StartTime;
                var to = entry.Date.Date + entry.EndTime;

                var exists = await _db.ClassEvents
                    .AnyAsync(ce => ce.Email == email
                        && ce.CourseId == courseId
                        && ce.From == from
                        && ce.To == to);

                if (exists)
                {
                    result.EntriesSkipped++;
                    continue;
                }

                // 5. Create ClassEvent
                var classEvent = new ClassEvent
                {
                    Email = email,
                    From = from,
                    To = to,
                    Recurring = true,
                    CourseId = courseId,
                    Location = entry.Location,
                    Duration = entry.Hours
                };
                _db.ClassEvents.Add(classEvent);
                await _db.SaveChangesAsync();
                result.EventsCreated++;

                // Track for result
                var key = entry.CourseCode!;
                if (courseMap.ContainsKey(key))
                    courseMap[key] = (courseId, course.CourseName, courseMap[key].eventCount + 1);
                else
                    courseMap[key] = (courseId, course.CourseName, 1);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to import entry for {entry.CourseCode}: {ex.Message}");
            }
        }

        result.Courses = courseMap.Values
            .Select(c => new ImportedCourseDto
            {
                CourseId = c.courseId,
                CourseName = c.name,
                EventCount = c.eventCount
            }).ToList();

        return result;
    }

    private static int ParseCourseId(string courseCode)
    {
        // "200233-10" → 20023310
        var parts = courseCode.Split('-');
        var main = int.Parse(parts[0]);
        var section = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return main * 100 + section;
    }

    private static string GetCurrentSemester()
    {
        var now = DateTime.Now;
        // Israeli academic year: A = Oct-Feb, B = Mar-Jul
        var suffix = now.Month >= 10 || now.Month <= 2 ? "A" : "B";
        var year = now.Month >= 10 ? now.Year : now.Year - 1;
        return $"{year}{suffix}";
    }

    // ── RTL Text Helpers ───────────────────────────────────────

    private static bool ContainsHebrew(string text)
        => text.Any(c => c >= '\u0590' && c <= '\u05FF');

    private static string FixRtlWord(string text)
    {
        if (string.IsNullOrEmpty(text) || !ContainsHebrew(text)) return text;
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// For a list of words sorted by X (left-to-right on page),
    /// Hebrew text reads right-to-left, so we reverse the order
    /// to get proper reading order.
    /// </summary>
    private static string BuildHebrewText(IEnumerable<PdfWord> words)
    {
        var list = words.ToList();
        // Reverse word order for RTL reading
        list.Reverse();
        return string.Join(" ", list.Select(w => w.Text));
    }

    // ── Internal Types ──────────────────────────────────────────

    private class PdfWord
    {
        public string Text { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private class RawEntry
    {
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public string? InstructorName { get; set; }
        public string? Location { get; set; }
        public decimal Hours { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<PdfWord> AllWords { get; set; } = new();
        public List<List<PdfWord>> Rows { get; set; } = new();
    }
}
