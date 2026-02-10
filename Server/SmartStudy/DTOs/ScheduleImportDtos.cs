namespace SmartStudy.DTOs;

public class ScheduleImportResultDto
{
    public int CoursesCreated { get; set; }
    public int EventsCreated { get; set; }
    public int EntriesSkipped { get; set; }
    public List<ImportedCourseDto> Courses { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ImportedCourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int EventCount { get; set; }
}
