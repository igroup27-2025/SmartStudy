using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly StressService _stressService;

    public NotificationsController(SmartStudyDbContext db, NotificationService notificationService, StressService stressService)
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
        var notifications = await _db.Notifications
            .Where(n => n.Email == email)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        var unreadCount = await _db.Notifications
            .CountAsync(n => n.Email == email && !n.IsRead);

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
        var count = await _db.Notifications.CountAsync(n => n.Email == email && !n.IsRead);
        return Ok(new { count });
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadDto dto)
    {
        var email = GetEmail();
        var notifications = await _db.Notifications
            .Where(n => n.Email == email && dto.NotificationIds.Contains(n.NotificationId))
            .ToListAsync();

        foreach (var n in notifications)
            n.IsRead = true;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var email = GetEmail();
        var unread = await _db.Notifications
            .Where(n => n.Email == email && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
            n.IsRead = true;

        await _db.SaveChangesAsync();
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
