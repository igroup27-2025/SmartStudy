using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

// API endpoints for in-app notifications: list, unread count, mark read, generate.
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns the user's notifications with the unread count.
    [HttpGet]
    public IActionResult GetAll()
    {
        var email = GetEmail();
        var (notifications, unreadCount) = Notification.GetByUser(email);

        return Ok(new NotificationListDto
        {
            Notifications = notifications.Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                RelatedEntityId = n.RelatedEntityId,
                RelatedEntityType = n.RelatedEntityType
            }).ToList(),
            UnreadCount = unreadCount
        });
    }

    // Returns just the count of unread notifications, for badge polling.
    [HttpGet("unread-count")]
    public IActionResult GetUnreadCount()
    {
        var email = GetEmail();
        var count = Notification.GetUnreadCount(email);
        return Ok(new { count });
    }

    // Marks the listed notifications as read for the current user.
    [HttpPost("mark-read")]
    public IActionResult MarkRead([FromBody] MarkReadDto dto)
    {
        var email = GetEmail();
        Notification.MarkRead(email, dto.NotificationIds);
        return Ok();
    }

    // Marks all of the user's notifications as read.
    [HttpPost("mark-all-read")]
    public IActionResult MarkAllRead()
    {
        var email = GetEmail();
        Notification.MarkAllRead(email);
        return Ok();
    }

    // Generates all notification types on demand (deadline, stress-overload, daily, weekly).
    // Each generator self-guards on its settings flag and dedup window, so calling them
    // here is safe even outside the background-service's morning window.
    [HttpPost("generate")]
    public IActionResult Generate()
    {
        var email = GetEmail();

        Notification.GenerateDeadline(email);

        try
        {
            var stress = UserModel.GetStressScore(email);
            if (stress != null)
                Notification.GenerateOverload(email, stress.Score);
        }
        catch { /* skip stress if it fails for this user */ }

        Notification.GenerateDailySummary(email);
        Notification.GenerateWeeklyPlanReminder(email);

        return Ok(new { generated = true });
    }
}
