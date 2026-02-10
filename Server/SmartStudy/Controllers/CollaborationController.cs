using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/collaboration")]
[Authorize]
public class CollaborationController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly SafeZoneService _safeZoneService;

    public CollaborationController(SmartStudyDbContext db, SafeZoneService safeZoneService)
    {
        _db = db;
        _safeZoneService = safeZoneService;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Get safe study zones (mutual free time with low stress) for a connection.
    /// </summary>
    [HttpGet("safe-zones")]
    public async Task<IActionResult> GetSafeZones([FromQuery] int connectionId)
    {
        var email = GetEmail();

        var friendship = await _db.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == connectionId &&
                (f.Email1 == email || f.Email2 == email) &&
                f.IsActive);

        if (friendship == null)
            return NotFound(new { message = "Friendship not found or not active" });

        var friendEmail = friendship.Email1 == email
            ? friendship.Email2
            : friendship.Email1;

        var zones = await _safeZoneService.GetSafeZonesAsync(email, friendEmail);
        return Ok(zones);
    }
}
