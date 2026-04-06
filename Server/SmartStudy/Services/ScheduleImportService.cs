using System.Globalization;
using System.Text.RegularExpressions;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using UglyToad.PdfPig;

namespace SmartStudy.Services;

public class ScheduleImportService
{
    private readonly DBservices _dal;
    private ColumnLayout? _columns;

    public ScheduleImportService(DBservices dal) => _dal = dal;

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

            // Detect column layout from header row (once per import)
            if (_columns == null)
                _columns = DetectColumns(rows);

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

    private ColumnLayout? DetectColumns(List<List<PdfWord>> rows)
    {
        foreach (var row in rows)
        {
            var roomW = row.FirstOrDefault(w => w.Text == "חדר");
            var instrW = row.FirstOrDefault(w => w.Text == "מרצה");
            var hoursW = row.FirstOrDefault(w => w.Text == "שעות");
            var courseWs = row.Where(w => w.Text == "שעור").ToList();

            if (roomW == null || instrW == null || courseWs.Count == 0) continue;

            var courseW = courseWs.Where(w => w.X > instrW.X).OrderBy(w => w.X).FirstOrDefault()
                          ?? courseWs.First();

            var rightOfRoom = row.Where(w => w.X > roomW.X + roomW.Width)
                                 .OrderBy(w => w.X).FirstOrDefault();
            var hashW = row.FirstOrDefault(w => w.Text == "#");

            return new ColumnLayout
            {
                RoomMaxX = rightOfRoom != null
                    ? (roomW.X + roomW.Width + rightOfRoom.X) / 2
                    : roomW.X + roomW.Width + 50,
                InstructorMinX = hoursW != null
                    ? (hoursW.X + hoursW.Width + instrW.X) / 2
                    : instrW.X - 30,
                InstructorMaxX = (instrW.X + instrW.Width + courseW.X) / 2,
                CourseMinX = (instrW.X + instrW.Width + courseW.X) / 2,
                CourseMaxX = hashW != null
                    ? hashW.X - 2
                    : 1000
            };
        }
        return null;
    }

    private List<RawEntry> ParseRows(List<List<PdfWord>> rows)
    {
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

        var entries = new List<RawEntry>();
        foreach (var rowGroup in entryRowGroups)
        {
            var entry = new RawEntry();
            entry.Rows = rowGroup;
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

        var codeMatch = Regex.Match(allText, @"(\d{6})-(\d{1,2})");
        if (codeMatch.Success)
            entry.CourseCode = codeMatch.Value;

        var dateMatch = Regex.Match(allText, @"(\d{2})/(\d{2})/(\d{4})");
        if (dateMatch.Success)
        {
            if (DateTime.TryParseExact(dateMatch.Value, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                entry.Date = date;
        }

        var timeMatches = Regex.Matches(allText, @"(\d{1,2}):(\d{2})");
        var times = new List<TimeSpan>();
        foreach (Match tm in timeMatches)
        {
            var h = int.Parse(tm.Groups[1].Value);
            var m = int.Parse(tm.Groups[2].Value);
            if (h >= 0 && h <= 23 && m >= 0 && m <= 59)
                times.Add(new TimeSpan(h, m, 0));
        }
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

        var hoursMatches = Regex.Matches(allText, @"(\d+)\.(\d{2})");
        foreach (Match hm in hoursMatches)
        {
            if (decimal.TryParse(hm.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var hrs) && hrs < 100)
            {
                entry.Hours = hrs;
                break;
            }
        }

        ExtractTextFields(entry);
    }

    private void ExtractTextFields(RawEntry entry)
    {
        if (_columns == null)
        {
            entry.CourseName = entry.CourseCode;
            return;
        }

        var roomRows = new List<List<PdfWord>>();
        var instrRows = new List<List<PdfWord>>();
        var courseRows = new List<List<PdfWord>>();

        foreach (var row in entry.Rows)
        {
            var rowRoom = new List<PdfWord>();
            var rowInstr = new List<PdfWord>();
            var rowCourse = new List<PdfWord>();

            foreach (var word in row)
            {
                if (ShouldFilterWord(word.Text)) continue;

                var cx = word.X + word.Width / 2;

                if (cx <= _columns.RoomMaxX)
                    rowRoom.Add(word);
                else if (cx >= _columns.InstructorMinX && cx <= _columns.InstructorMaxX)
                    rowInstr.Add(word);
                else if (cx >= _columns.CourseMinX && cx <= _columns.CourseMaxX)
                    rowCourse.Add(word);
            }

            if (rowRoom.Count > 0) roomRows.Add(rowRoom);
            if (rowInstr.Count > 0) instrRows.Add(rowInstr);
            if (rowCourse.Count > 0) courseRows.Add(rowCourse);
        }

        var courseName = BuildColumnText(courseRows);
        var instructor = BuildColumnText(instrRows);
        var location = BuildColumnText(roomRows);

        if (!string.IsNullOrWhiteSpace(courseName))
        {
            courseName = Regex.Replace(courseName, @"\bשיעור\b", " ");
            courseName = Regex.Replace(courseName, @"\bבזום\b", " ");
            courseName = Regex.Replace(courseName, @"\s+", " ").Trim().Trim('-', ' ');
            entry.CourseName = courseName;
        }

        if (!string.IsNullOrWhiteSpace(instructor))
            entry.InstructorName = instructor.TrimEnd(',').Trim();

        if (!string.IsNullOrWhiteSpace(location))
            entry.Location = location;
    }

    private static bool ShouldFilterWord(string text)
    {
        var t = text.Trim();
        if (string.IsNullOrEmpty(t)) return true;
        if (Regex.IsMatch(t, @"^\d{6}-\d{1,2}$")) return true;
        if (Regex.IsMatch(t, @"^\d{2}/\d{2}/\d{4}$")) return true;
        if (Regex.IsMatch(t, @"^\d{1,2}:\d{2}$")) return true;
        if (Regex.IsMatch(t, @"^\d+\.\d{2}$")) return true;
        if (IsHebrewDatePart(t)) return true;
        if (t.Length == 1 && "אבגדהוש".Contains(t[0])) return true;
        if (t.Contains("סה\"כ") || t == "שעות" || t == ":") return true;
        return false;
    }

    private static string BuildColumnText(List<List<PdfWord>> rowGroups)
    {
        var parts = new List<string>();
        foreach (var row in rowGroups)
        {
            if (row.Count > 0)
                parts.Add(BuildHebrewText(row));
        }
        var text = string.Join(" ", parts);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool IsHebrewDatePart(string text)
    {
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
                var courseId = ParseCourseId(entry.CourseCode!);
                var courseName = entry.CourseName ?? entry.CourseCode ?? "Unknown Course";

                var course = await _dal.GetCourseByIdAsync(courseId);
                if (course == null)
                {
                    await _dal.CreateCourseAsync(courseId, courseName, entry.Hours, null, GetCurrentSemester(), null);
                    result.CoursesCreated++;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(courseName) && courseName != entry.CourseCode)
                        await _dal.UpdateCourseAsync(courseId, courseName: courseName);
                }

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

                if (!await _dal.UserCourseExistsAsync(email, courseId))
                {
                    await _dal.CreateUserCourseAsync(email, courseId);
                }

                var from = entry.Date.Date + entry.StartTime;
                var to = entry.Date.Date + entry.EndTime;

                if (await _dal.ClassEventExistsAsync(email, courseId, from, to))
                {
                    result.EntriesSkipped++;
                    continue;
                }

                await _dal.CreateClassEventAsync(email, from, to, true, null, courseId, entry.Location, entry.Hours);
                result.EventsCreated++;

                var key = entry.CourseCode!;
                if (courseMap.ContainsKey(key))
                    courseMap[key] = (courseId, courseName, courseMap[key].eventCount + 1);
                else
                    courseMap[key] = (courseId, courseName, 1);
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
        var parts = courseCode.Split('-');
        var main = int.Parse(parts[0]);
        var section = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return main * 100 + section;
    }

    private static string GetCurrentSemester()
    {
        var now = DateTime.Now;
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
        return Regex.Replace(new string(chars), @"\d+", m =>
            new string(m.Value.Reverse().ToArray()));
    }

    private static string BuildHebrewText(IEnumerable<PdfWord> words)
    {
        var list = words.ToList();
        list.Reverse();
        return string.Join(" ", list.Select(w => w.Text));
    }

    // ── Internal Types ──────────────────────────────────────────

    private class ColumnLayout
    {
        public double RoomMaxX { get; set; }
        public double InstructorMinX { get; set; }
        public double InstructorMaxX { get; set; }
        public double CourseMinX { get; set; }
        public double CourseMaxX { get; set; }
    }

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
