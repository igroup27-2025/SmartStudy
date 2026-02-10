namespace SmartStudy.DTOs;

public class WeeklySuggestionsDto
{
    public List<SuggestionDto> Suggestions { get; set; } = new();
    public List<FocusTaskDto> FocusTasks { get; set; } = new();
    public double TotalStudyHoursNeeded { get; set; }
    public double AvailableStudyHours { get; set; }
}

public class SuggestionDto
{
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Icon { get; set; } = null!;
}

public class FocusTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public double HoursNeeded { get; set; }
    public int DaysUntilDue { get; set; }
    public string? Priority { get; set; }
}
