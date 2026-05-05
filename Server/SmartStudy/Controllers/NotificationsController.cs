using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

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

    [HttpGet("unread-count")]
    public IActionResult GetUnreadCount()
    {
        var email = GetEmail();
        var count = Notification.GetUnreadCount(email);
        return Ok(new { count });
    }

    [HttpPost("mark-read")]
    public IActionResult MarkRead([FromBody] MarkReadDto dto)
    {
        var email = GetEmail();
        Notification.MarkRead(email, dto.NotificationIds);
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public IActionResult MarkAllRead()
    {
        var email = GetEmail();
        Notification.MarkAllRead(email);
        return Ok();
    }

    [HttpPost("generate")]
    public IActionResult Generate()
    {
        var email = GetEmail();

        Notification.GenerateDeadline(email);

        var stress = UserModel.GetStressScore(email);
        Notification.GenerateOverload(email, stress.Score);

        return Ok(new { generated = true });
    }
}
