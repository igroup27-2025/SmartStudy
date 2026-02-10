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

    private static (string, string) NormalizePair(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? (a, b) : (b, a);

    /// <summary>
    /// Get all connections (accepted friends + pending incoming requests) for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();

        // Pending incoming requests
        var pendingRequests = await _db.FriendRequests
            .Include(r => r.Requester)
            .Where(r => r.AddresseeEmail == email && r.Status == "Pending")
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        // Sent pending requests
        var sentRequests = await _db.FriendRequests
            .Include(r => r.Addressee)
            .Where(r => r.RequesterEmail == email && r.Status == "Pending")
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        // Active friendships
        var friendships = await _db.Friendships
            .Include(f => f.User1)
            .Include(f => f.User2)
            .Where(f => (f.Email1 == email || f.Email2 == email) && f.IsActive)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var result = new List<ConnectionDto>();

        // Map pending incoming → status "pending"
        foreach (var r in pendingRequests)
        {
            result.Add(new ConnectionDto
            {
                ConnectionId = r.RequestId,
                FriendEmail = r.Requester.Email,
                FriendName = $"{r.Requester.FirstName} {r.Requester.LastName}",
                Status = "pending",
                ConnectedDate = r.RequestedAt
            });
        }

        // Map sent pending → status "sent"
        foreach (var r in sentRequests)
        {
            result.Add(new ConnectionDto
            {
                ConnectionId = r.RequestId,
                FriendEmail = r.Addressee.Email,
                FriendName = $"{r.Addressee.FirstName} {r.Addressee.LastName}",
                Status = "sent",
                ConnectedDate = r.RequestedAt
            });
        }

        // Map active friendships → status "accepted"
        foreach (var f in friendships)
        {
            var friend = f.Email1 == email ? f.User2 : f.User1;
            result.Add(new ConnectionDto
            {
                ConnectionId = f.FriendshipId,
                FriendEmail = friend.Email,
                FriendName = $"{friend.FirstName} {friend.LastName}",
                Status = "accepted",
                ConnectedDate = f.CreatedAt
            });
        }

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

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (targetUser == null)
            return NotFound(new { message = "User not found" });

        // Check existing pending request (either direction)
        var existingRequest = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.Status == "Pending" &&
            ((r.RequesterEmail == email && r.AddresseeEmail == dto.Email) ||
             (r.RequesterEmail == dto.Email && r.AddresseeEmail == email)));

        if (existingRequest != null)
            return BadRequest(new { message = "A pending request already exists" });

        // Check existing active friendship
        var (e1, e2) = NormalizePair(email, dto.Email);
        var existingFriendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.Email1 == e1 && f.Email2 == e2 && f.IsActive);

        if (existingFriendship != null)
            return BadRequest(new { message = "Already friends" });

        var request = new FriendRequest
        {
            RequesterEmail = email,
            AddresseeEmail = dto.Email,
            Status = "Pending",
            RequestedAt = DateTime.Now
        };

        _db.FriendRequests.Add(request);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, new { request.RequestId, status = "pending" });
    }

    /// <summary>
    /// Accept a pending connection request. Creates a Friendship record.
    /// </summary>
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var email = GetEmail();

        var request = await _db.FriendRequests
            .FirstOrDefaultAsync(r => r.RequestId == id && r.AddresseeEmail == email && r.Status == "Pending");

        if (request == null)
            return NotFound(new { message = "Pending request not found" });

        request.Status = "Accepted";
        request.RespondedAt = DateTime.Now;

        // Create friendship with normalized email pair
        var (e1, e2) = NormalizePair(request.RequesterEmail, request.AddresseeEmail);
        var friendship = new Friendship
        {
            Email1 = e1,
            Email2 = e2,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        _db.Friendships.Add(friendship);
        await _db.SaveChangesAsync();

        return Ok(new { friendshipId = friendship.FriendshipId, status = "accepted" });
    }

    /// <summary>
    /// Decline a pending connection request.
    /// </summary>
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(int id)
    {
        var email = GetEmail();

        var request = await _db.FriendRequests
            .FirstOrDefaultAsync(r => r.RequestId == id && r.AddresseeEmail == email && r.Status == "Pending");

        if (request == null)
            return NotFound(new { message = "Pending request not found" });

        request.Status = "Rejected";
        request.RespondedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Remove an existing friendship (soft delete).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var email = GetEmail();

        var friendship = await _db.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == id &&
                (f.Email1 == email || f.Email2 == email) && f.IsActive);

        if (friendship == null)
            return NotFound();

        friendship.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
