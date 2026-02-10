using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class NotificationService
{
    private readonly SmartStudyDbContext _db;

    public NotificationService(SmartStudyDbContext db)
    {
        _db = db;
    }

    public async Task GenerateDeadlineNotificationsAsync(string email)
    {
        var now = DateTime.Now;
        var in24h = now.AddHours(24);

        var urgentTasks = await _db.Tasks
            .Include(t => t.Course)
            .Where(t => t.Email == email && !t.IsCompleted
                && t.DueDate.HasValue && t.DueDate > now && t.DueDate <= in24h)
            .ToListAsync();

        foreach (var task in urgentTasks)
        {
            if (await IsDuplicate(email, "deadline", task.TaskId, "Task"))
                continue;

            _db.Notifications.Add(new Notification
            {
                Email = email,
                Type = "deadline",
                Title = "Task Due Soon",
                Message = $"\"{task.Title}\" ({task.Course?.CourseName}) is due within 24 hours!",
                CreatedAt = now,
                RelatedEntityId = task.TaskId,
                RelatedEntityType = "Task"
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task GenerateOverloadNotificationAsync(string email, double stressScore)
    {
        if (stressScore <= 70) return;

        if (await IsDuplicate(email, "overload", null, null))
            return;

        _db.Notifications.Add(new Notification
        {
            Email = email,
            Type = "overload",
            Title = "High Stress Alert",
            Message = $"Your stress level is at {stressScore:F0}%. Consider rescheduling or asking for help.",
            CreatedAt = DateTime.Now,
            RelatedEntityType = "Stress"
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateSharedTaskInviteNotificationAsync(string recipientEmail, string senderName, int taskId, string taskTitle)
    {
        _db.Notifications.Add(new Notification
        {
            Email = recipientEmail,
            Type = "shared_task_invite",
            Title = "Shared Task Invitation",
            Message = $"{senderName} invited you to collaborate on \"{taskTitle}\".",
            CreatedAt = DateTime.Now,
            RelatedEntityId = taskId,
            RelatedEntityType = "SharedTask"
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateSharedTaskResponseNotificationAsync(string recipientEmail, string responderName, int taskId, string taskTitle, bool accepted)
    {
        _db.Notifications.Add(new Notification
        {
            Email = recipientEmail,
            Type = "shared_task_response",
            Title = accepted ? "Task Accepted" : "Task Declined",
            Message = $"{responderName} {(accepted ? "accepted" : "declined")} the shared task \"{taskTitle}\".",
            CreatedAt = DateTime.Now,
            RelatedEntityId = taskId,
            RelatedEntityType = "SharedTask"
        });

        await _db.SaveChangesAsync();
    }

    private async Task<bool> IsDuplicate(string email, string type, int? entityId, string? entityType)
    {
        var since = DateTime.Now.AddHours(-24);
        return await _db.Notifications.AnyAsync(n =>
            n.Email == email && n.Type == type
            && n.CreatedAt > since
            && n.RelatedEntityId == entityId
            && n.RelatedEntityType == entityType);
    }
}
