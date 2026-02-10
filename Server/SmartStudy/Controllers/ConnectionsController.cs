using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize]
public class ConnectionsController : ControllerBase
{
    private readonly SmartStudyDbContext _db;

    public ConnectionsController(SmartStudyDbContext db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Get all connections (accepted + pending incoming) for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();

        var connections = await _db.StudyConnections
            .Include(c => c.Requester)
            .Include(c => c.Receiver)
            .Where(c => c.RequesterEmail == email || c.ReceiverEmail == email)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = connections.Select(c =>
        {
            var isRequester = c.RequesterEmail == email;
            var friend = isRequester ? c.Receiver : c.Requester;

            // For pending requests where current user is the requester, mark as "sent"
            var status = c.Status.ToLower();
            if (status == "pending" && isRequester)
                status = "sent";

            return new ConnectionDto
            {
                ConnectionId = c.ConnectionId,
                FriendEmail = friend.Email,
                FriendName = $"{friend.FirstName} {friend.LastName}",
                Status = status,
                ConnectedDate = c.AcceptedAt ?? c.CreatedAt
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Send a connection invitation to another user by email.
    /// </summary>
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteConnectionDto dto)
    {
        var email = GetEmail();

        if (dto.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "You cannot invite yourself" });

        // Check target user exists
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (targetUser == null)
            return NotFound(new { message = "User not found" });

        // Check for existing connection
        var existing = await _db.StudyConnections.FirstOrDefaultAsync(c =>
            (c.RequesterEmail == email && c.ReceiverEmail == dto.Email) ||
            (c.RequesterEmail == dto.Email && c.ReceiverEmail == email));

        if (existing != null)
            return BadRequest(new { message = "Connection already exists" });

        var connection = new StudyConnection
        {
            RequesterEmail = email,
            ReceiverEmail = dto.Email,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        _db.StudyConnections.Add(connection);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, new { connection.ConnectionId, status = "pending" });
    }

    /// <summary>
    /// Accept a pending connection request.
    /// </summary>
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var email = GetEmail();

        var connection = await _db.StudyConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == id && c.ReceiverEmail == email && c.Status == "Pending");

        if (connection == null)
            return NotFound(new { message = "Pending request not found" });

        connection.Status = "Accepted";
        connection.AcceptedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new { connection.ConnectionId, status = "accepted" });
    }

    /// <summary>
    /// Decline a pending connection request.
    /// </summary>
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(int id)
    {
        var email = GetEmail();

        var connection = await _db.StudyConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == id && c.ReceiverEmail == email && c.Status == "Pending");

        if (connection == null)
            return NotFound(new { message = "Pending request not found" });

        _db.StudyConnections.Remove(connection);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Remove an existing connection.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var email = GetEmail();

        var connection = await _db.StudyConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == id &&
                (c.RequesterEmail == email || c.ReceiverEmail == email));

        if (connection == null)
            return NotFound();

        _db.StudyConnections.Remove(connection);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
