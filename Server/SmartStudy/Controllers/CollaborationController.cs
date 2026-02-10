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

        var connection = await _db.StudyConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == connectionId &&
                (c.RequesterEmail == email || c.ReceiverEmail == email) &&
                c.Status == "Accepted");

        if (connection == null)
            return NotFound(new { message = "Connection not found or not accepted" });

        var friendEmail = connection.RequesterEmail == email
            ? connection.ReceiverEmail
            : connection.RequesterEmail;

        var zones = await _safeZoneService.GetSafeZonesAsync(email, friendEmail);
        return Ok(zones);
    }
}
