using SmartStudy.DAL;

namespace SmartStudy.Models;

// Aggregates dashboard queries that don't fit naturally onto a single domain entity.
public static class Dashboard
{
    // Returns the IDs of every course the user is enrolled in.
    public static List<int> GetCourseIdsByEmail(string email)
    {
        DBservices db = new DBservices();
        return db.GetCourseIdsByEmail(email);
    }

    // Returns exams for the user's courses on or after the given date.
    public static List<SchedulingExamRow> GetUpcomingExams(string email, DateTime fromDate)
    {
        DBservices db = new DBservices();
        return db.GetUpcomingExams(email, fromDate);
    }

    // Returns the user's incomplete tasks alongside their scheduled task-event slots.
    public static (List<DashboardTaskRow> Tasks, List<DashboardTaskEventRow> TaskEvents) GetIncompleteTasksWithEvents(string email)
    {
        DBservices db = new DBservices();
        return db.GetIncompleteTasksWithEvents(email);
    }

    // Returns completed tasks with estimated/actual hours used to learn per-course time bias.
    public static List<MLCompletedTaskRow> GetCompletedTasksForML(string email)
    {
        DBservices db = new DBservices();
        return db.GetCompletedTasksForML(email);
    }
}

// ───── Dashboard aggregate DTO (from DashboardDtos.cs) ─────────────

// Wire-format payload returned by GET /api/dashboard.
public class DashboardDto
{
    public StressScoreDto Stress { get; set; } = null!;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int UpcomingExams { get; set; }
    public int TotalCourses { get; set; }
    public List<TaskDto> UpcomingDeadlines { get; set; } = new();
    public List<ExamDto> NextExams { get; set; } = new();
    public List<EventDto> TodayEvents { get; set; } = new();

    // Scheduling data
    public int UnscheduledTaskCount { get; set; }
    public double TodayWorkloadHours { get; set; }
    public double WeeklyWorkloadHours { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public TaskDto? NextSuggestedTask { get; set; }
    public List<TaskDto> NeedsReviewTasks { get; set; } = new();
    public List<TaskDto> OverdueTasks { get; set; } = new();
    public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
}
