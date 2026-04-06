using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly DBservices _db;
    private readonly NotificationService _notificationService;
    private readonly StressService _stressService;

    public NotificationsController(DBservices db, NotificationService notificationService, StressService stressService)
    {
        _db = db;
        _notificationService = notificationService;
        _stressService = stressService;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var (notifications, unreadCount) = await _db.GetNotificationsByUserAsync(email);

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
    public async Task<IActionResult> GetUnreadCount()
    {
        var email = GetEmail();
        var count = await _db.GetUnreadNotificationCountAsync(email);
        return Ok(new { count });
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadDto dto)
    {
        var email = GetEmail();
        await _db.MarkNotificationsReadAsync(email, dto.NotificationIds);
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var email = GetEmail();
        await _db.MarkAllNotificationsReadAsync(email);
        return Ok();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate()
    {
        var email = GetEmail();

        // Generate deadline notifications
        await _notificationService.GenerateDeadlineNotificationsAsync(email);

        // Generate overload notification based on stress
        var stress = await _stressService.GetStressScoreAsync(email);
        await _notificationService.GenerateOverloadNotificationAsync(email, stress.Score);

        return Ok(new { generated = true });
    }
}
