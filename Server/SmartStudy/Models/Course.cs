using System.Globalization;
using System.Text.RegularExpressions;
using SmartStudy.DAL;
using UglyToad.PdfPig;

namespace SmartStudy.Models;

// Course entity plus folded-in BLL methods for course CRUD and the PDF schedule importer.
public class Course
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }

    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }

    // Navigation properties
    public Instructor? Instructor { get; set; }
    public ICollection<UserCourse> UserCourses { get; set; } = new List<UserCourse>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<StudentTask> Tasks { get; set; } = new List<StudentTask>();
    public ICollection<ClassEvent> ClassEvents { get; set; } = new List<ClassEvent>();

    // ───── CoursesBLL methods folded in ──────────────────────────────────

    // Returns courses the user is enrolled in with task/exam counts and partner data.
    public static List<CourseWithEnrollment> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetCoursesByUser(email);
    }

    // Loads a single course by ID from the global courses table.
    public static Course? GetById(int courseId)
    {
        DBservices db = new DBservices();
        return db.GetCourseById(courseId);
    }

    // Returns true when the user is enrolled in the course (used to authorize updates).
    public static bool UserCourseExists(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.UserCourseExists(email, courseId);
    }

    // Returns the highest existing course ID, used to allocate the next manual course.
    public static int GetMaxCourseId()
    {
        DBservices db = new DBservices();
        return db.GetMaxCourseId();
    }

    // Inserts a new course row into the global courses table.
    public static void Create(int courseId, string courseName, decimal? weeklyHours, decimal? credits, string? semester, int? instructorId)
    {
        DBservices db = new DBservices();
        db.CreateCourse(courseId, courseName, weeklyHours, credits, semester, instructorId);
    }

    // Enrolls the user in the course by inserting into the UserCourses junction.
    public static void CreateUserCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        db.CreateUserCourse(email, courseId);
    }

    // Updates any subset of a course's fields (each parameter optional).
    public static void Update(int courseId, string? courseName = null, decimal? weeklyHours = null, decimal? credits = null,
        string? semester = null, int? instructorId = null, double? defaultTaskEstimatedHours = null,
        double? examPrepHoursPerDay = null, int? examPrepDays = null)
    {
        DBservices db = new DBservices();
        db.UpdateCourse(courseId, courseName, weeklyHours, credits, semester, instructorId,
            defaultTaskEstimatedHours, examPrepHoursPerDay, examPrepDays);
    }

    // Sets whether the user's tasks for this course should be auto-shared with their study partner.
    public static void UpdateSharedByDefault(string email, int courseId, bool sharedByDefault)
    {
        DBservices db = new DBservices();
        db.UpdateSharedByDefault(email, courseId, sharedByDefault);
    }

    // Sets or clears the study partner assigned to the user's enrollment.
    public static void UpdateStudyPartner(string email, int courseId, string? partnerEmail)
    {
        DBservices db = new DBservices();
        db.UpdateStudyPartner(email, courseId, partnerEmail);
    }

    // Removes the user's enrollment in the course (does not delete the global course).
    public static void DeleteUserCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        db.DeleteUserCourse(email, courseId);
    }

    // ───── ScheduleImportBLL methods folded in ──────────────────────────

    // Looks up an instructor by case-insensitive name.
    public static Instructor? FindInstructorByName(string name)
    {
        DBservices db = new DBservices();
        return db.FindInstructorByName(name);
    }

    // Inserts a new instructor row and returns its ID.
    public static int CreateInstructor(string name)
    {
        DBservices db = new DBservices();
        return db.CreateInstructor(name);
    }

    // Sets or replaces the instructor on a course.
    public static void UpdateCourseInstructor(int courseId, int instructorId)
    {
        DBservices db = new DBservices();
        db.UpdateCourseInstructor(courseId, instructorId);
    }

    // Returns true if a class event already exists at this exact time slot for the course.
    public static bool ClassEventExists(string email, int courseId, DateTime from, DateTime to)
    {
        DBservices db = new DBservices();
        return db.ClassEventExists(email, courseId, from, to);
    }

    // Inserts a class event into the user's calendar and returns the new event ID.
    public static int CreateClassEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        DBservices db = new DBservices();
        return db.CreateClassEvent(email, from, to, recurring, recurrenceEndDate, courseId, location, duration);
    }

    // ───── Schedule import (from ScheduleImportService) ──────────────────

    // Parses a Hebrew Ruppin schedule PDF and persists each entry as a course/class event.
    public static ScheduleImportResultDto ImportSchedule(Stream pdfStream, string email)
    {
        var db = new DBservices();
        ColumnLayout? columns = null;

        var raw = ParsePdf(pdfStream, ref columns);
        var filtered = raw.Where(e => e.Hours > 0).ToList();
        var skipped = raw.Count - filtered.Count;

        var entries = MergeDuplicates(filtered);

        var result = SaveImportToDatabase(db, entries, email);
        result.EntriesSkipped += skipped;
        return result;
    }

    // Walks every page of the PDF, groups words into rows, and extracts schedule entries.
    private static List<RawEntry> ParsePdf(Stream pdfStream, ref ColumnLayout? columns)
    {
        using var document = PdfDocument.Open(pdfStream);
        var allEntries = new List<RawEntry>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            var pageHeight = page.Height;
            var rows = GroupIntoRows(words, pageHeight);

            if (columns == null)
                columns = DetectColumns(rows);

            allEntries.AddRange(ParseRows(rows, columns));
        }

        return allEntries;
    }

    // Sorts PDF words by Y coordinate and groups them into rows within a small Y tolerance.
    private static List<List<PdfWord>> GroupIntoRows(List<UglyToad.PdfPig.Content.Word> words, double pageHeight)
    {
        var pdfWords = words.Select(w => new PdfWord
        {
            Text = FixRtlWord(w.Text),
            X = w.BoundingBox.Left,
            Y = pageHeight - w.BoundingBox.Top,
            Width = w.BoundingBox.Width,
            Height = w.BoundingBox.Height
        }).OrderBy(w => w.Y).ToList();

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

    // Finds the header row and uses Hebrew column titles to compute X-bounds for each column.
    private static ColumnLayout? DetectColumns(List<List<PdfWord>> rows)
    {
        foreach (var row in rows)
        {
            var roomW = row.FirstOrDefault(w => w.Text == "חדר");
            var instrW = row.FirstOrDefault(w => w.Text == "מרצה");
            var hoursW = row.FirstOrDefault(w => w.Text == "שעות");
            var courseWs = row.Where(w => w.Text == "שעור").ToList();

            if (roomW == null || instrW == null || courseWs.Count == 0) continue;

            var courseW = courseWs.Where(w => w.X > instrW.X).OrderBy(w => w.X).FirstOrDefault() ?? courseWs.First();

            var rightOfRoom = row.Where(w => w.X > roomW.X + roomW.Width).OrderBy(w => w.X).FirstOrDefault();
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
                CourseMaxX = hashW != null ? hashW.X - 2 : 1000
            };
        }
        return null;
    }

    // Groups consecutive rows by date and extracts a RawEntry per schedule item.
    private static List<RawEntry> ParseRows(List<List<PdfWord>> rows, ColumnLayout? columns)
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
            ExtractFromWords(entry, columns);

            if (!string.IsNullOrEmpty(entry.CourseCode) && entry.Date != default)
                entries.Add(entry);
        }

        return entries;
    }

    // Pulls course code, date, time range, and credit hours out of a row group's text.
    private static void ExtractFromWords(RawEntry entry, ColumnLayout? columns)
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

        ExtractTextFields(entry, columns);
    }

    // Bins each word into Room/Instructor/Course columns by X coordinate and assembles text fields.
    private static void ExtractTextFields(RawEntry entry, ColumnLayout? columns)
    {
        if (columns == null)
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

                if (cx <= columns.RoomMaxX)
                    rowRoom.Add(word);
                else if (cx >= columns.InstructorMinX && cx <= columns.InstructorMaxX)
                    rowInstr.Add(word);
                else if (cx >= columns.CourseMinX && cx <= columns.CourseMaxX)
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

    // Returns true for words that aren't part of any text column (codes, dates, times, etc.).
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

    // Joins multi-row column words into a single Hebrew-aware text string.
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

    // Returns true if the word is part of a Hebrew calendar date (months, day numerals, year).
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

    // Collapses repeated entries for the same course on the same date, keeping the longest one.
    private static List<RawEntry> MergeDuplicates(List<RawEntry> entries)
    {
        return entries
            .GroupBy(e => (e.CourseCode, e.Date.Date))
            .Select(g => g.OrderByDescending(e => e.EndTime - e.StartTime).First())
            .ToList();
    }

    // Persists each parsed entry as a course/instructor/enrollment/class-event with dedupe.
    private static ScheduleImportResultDto SaveImportToDatabase(DBservices db, List<RawEntry> entries, string email)
    {
        var result = new ScheduleImportResultDto();
        var courseMap = new Dictionary<string, (int courseId, string name, int eventCount)>();

        foreach (var entry in entries)
        {
            try
            {
                var courseId = ParseCourseId(entry.CourseCode!);
                var courseName = entry.CourseName ?? entry.CourseCode ?? "Unknown Course";

                var course = db.GetCourseById(courseId);
                if (course == null)
                {
                    db.CreateCourse(courseId, courseName, entry.Hours, null, GetCurrentSemester(), null);
                    result.CoursesCreated++;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(courseName) && courseName != entry.CourseCode)
                        db.UpdateCourse(courseId, courseName: courseName);
                }

                if (!string.IsNullOrEmpty(entry.InstructorName))
                {
                    var instructor = db.FindInstructorByName(entry.InstructorName);
                    if (instructor == null)
                    {
                        var instructorId = db.CreateInstructor(entry.InstructorName);
                        db.UpdateCourseInstructor(courseId, instructorId);
                    }
                    else
                    {
                        db.UpdateCourseInstructor(courseId, instructor.InstructorId);
                    }
                }

                if (!db.UserCourseExists(email, courseId))
                {
                    db.CreateUserCourse(email, courseId);
                }

                var from = entry.Date.Date + entry.StartTime;
                var to = entry.Date.Date + entry.EndTime;

                if (db.ClassEventExists(email, courseId, from, to))
                {
                    result.EntriesSkipped++;
                    continue;
                }

                db.CreateClassEvent(email, from, to, true, null, courseId, entry.Location, entry.Hours);
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

    // Converts the "######-##" Ruppin course code to a numeric course ID.
    private static int ParseCourseId(string courseCode)
    {
        var parts = courseCode.Split('-');
        var main = int.Parse(parts[0]);
        var section = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return main * 100 + section;
    }

    // Computes the current academic semester code (e.g. "2025A" or "2025B").
    private static string GetCurrentSemester()
    {
        var now = DateTime.Now;
        var suffix = now.Month >= 10 || now.Month <= 2 ? "A" : "B";
        var year = now.Month >= 10 ? now.Year : now.Year - 1;
        return $"{year}{suffix}";
    }

    // Returns true if any character is in the Hebrew Unicode block.
    private static bool ContainsHebrew(string text)
        => text.Any(c => c >= '\u0590' && c <= '\u05FF');

    // Reverses Hebrew word characters from PdfPig's RTL output back to logical order.
    private static string FixRtlWord(string text)
    {
        if (string.IsNullOrEmpty(text) || !ContainsHebrew(text)) return text;
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return Regex.Replace(new string(chars), @"\d+", m =>
            new string(m.Value.Reverse().ToArray()));
    }

    // Reverses word order to render an RTL line as a left-to-right Hebrew string.
    private static string BuildHebrewText(IEnumerable<PdfWord> words)
    {
        var list = words.ToList();
        list.Reverse();
        return string.Join(" ", list.Select(w => w.Text));
    }

    // ───── PDF import internal types ────────────────────────────

    // X-coordinate boundaries of the four schedule-PDF columns.
    private class ColumnLayout
    {
        public double RoomMaxX { get; set; }
        public double InstructorMinX { get; set; }
        public double InstructorMaxX { get; set; }
        public double CourseMinX { get; set; }
        public double CourseMaxX { get; set; }
    }

    // Lightweight PDF word with text and pixel coordinates for layout analysis.
    private class PdfWord
    {
        public string Text { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    // One unsaved schedule entry parsed from the PDF (one course on one date).
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

// ───── Course DTOs (from CourseDtos.cs) ────────────────────────────

// Wire-format response shape for the Courses API.
public class CourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public int TaskCount { get; set; }
    public int ExamCount { get; set; }
    public string? StudyPartnerEmail { get; set; }
    public string? StudyPartnerName { get; set; }
    public bool SharedByDefault { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

// Request body for PUT /api/courses/{id}/partner.
public class SetStudyPartnerDto
{
    public string? Email { get; set; }
}

// Request body for POST /api/courses.
public class CreateCourseDto
{
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
}

// Request body for PUT /api/courses/{id} — all fields optional.
public class UpdateCourseDto
{
    public string? CourseName { get; set; }
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
    public bool? SharedByDefault { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

// ───── Schedule import DTOs (from ScheduleImportDtos.cs) ───────────

// Result payload from POST /api/schedule/import (counts and per-course summary).
public class ScheduleImportResultDto
{
    public int CoursesCreated { get; set; }
    public int EventsCreated { get; set; }
    public int EntriesSkipped { get; set; }
    public List<ImportedCourseDto> Courses { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Per-course summary inside ScheduleImportResultDto.
public class ImportedCourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int EventCount { get; set; }
}
