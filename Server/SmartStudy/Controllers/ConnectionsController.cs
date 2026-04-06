using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SmartStudy.DAL;
using SmartStudy.DTOs;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize]
public class ConnectionsController : ControllerBase
{
    private readonly DBservices _db;

    public ConnectionsController(DBservices db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Get all connections (accepted friends + pending incoming requests) for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();

        // Pending friend requests (incoming + sent)
        var requests = await _db.GetFriendRequestsByUserAsync(email);

        // Active friendships
        var friendships = await _db.GetFriendshipsByUserAsync(email);

        var result = new List<ConnectionDto>();

        // Map pending requests
        foreach (var r in requests)
        {
            var status = r.AddresseeEmail == email ? "pending" : "sent";
            result.Add(new ConnectionDto
            {
                ConnectionId = r.RequestId,
                FriendEmail = r.FriendEmail,
                FriendName = $"{r.FirstName} {r.LastName}",
                Status = status,
                ConnectedDate = r.RequestedAt
            });
        }

        // Map active friendships → status "accepted"
        foreach (var f in friendships)
        {
            result.Add(new ConnectionDto
            {
                ConnectionId = f.FriendshipId,
                FriendEmail = f.FriendEmail,
                FriendName = $"{f.FirstName} {f.LastName}",
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

        try
        {
            var requestId = await _db.CreateFriendRequestAsync(email, dto.Email);
            return CreatedAtAction(nameof(GetAll), new { }, new { requestId, status = "pending" });
        }
        catch (SqlException ex) when (ex.Number == 50000)
        {
            // SP raises errors with specific messages
            var msg = ex.Message;
            if (msg.Contains("Cannot invite yourself"))
                return BadRequest(new { message = "You cannot invite yourself" });
            if (msg.Contains("User not found"))
                return NotFound(new { message = "User not found" });
            if (msg.Contains("pending request already exists"))
                return BadRequest(new { message = "A pending request already exists" });
            if (msg.Contains("Already friends"))
                return BadRequest(new { message = "Already friends" });
            return BadRequest(new { message = msg });
        }
    }

    /// <summary>
    /// Accept a pending connection request. Creates a Friendship record.
    /// </summary>
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var email = GetEmail();

        var updated = await _db.UpdateFriendRequestStatusAsync(id, email, "Accepted");
        if (updated == null || updated.Status != "Accepted")
            return NotFound(new { message = "Pending request not found" });

        // Create friendship
        var friendshipId = await _db.CreateFriendshipAsync(updated.RequesterEmail, updated.AddresseeEmail);

        return Ok(new { friendshipId, status = "accepted" });
    }

    /// <summary>
    /// Decline a pending connection request.
    /// </summary>
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(int id)
    {
        var email = GetEmail();

        var updated = await _db.UpdateFriendRequestStatusAsync(id, email, "Rejected");
        if (updated == null || updated.Status != "Rejected")
            return NotFound(new { message = "Pending request not found" });

        return NoContent();
    }

    /// <summary>
    /// Remove an existing friendship (soft delete).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var email = GetEmail();

        var removed = await _db.DeactivateFriendshipAsync(id, email);
        if (!removed)
            return NotFound();

        return NoContent();
    }
}
