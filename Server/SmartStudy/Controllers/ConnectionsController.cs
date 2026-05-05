using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for managing peer connections (friend requests and friendships).
[ApiController]
[Route("api/connections")]
[Authorize]
public class ConnectionsController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Get all connections (accepted friends + pending incoming requests) for the current user.
    [HttpGet]
    public IActionResult GetAll()
    {
        var email = GetEmail();

        var requests = Friendship.GetFriendRequestsByUser(email);
        var friendships = Friendship.GetByUser(email);

        var result = new List<ConnectionDto>();

        foreach (var r in requests)
        {
            var status = string.Equals(r.AddresseeEmail, email, StringComparison.OrdinalIgnoreCase) ? "pending" : "sent";
            result.Add(new ConnectionDto
            {
                ConnectionId = r.RequestId,
                FriendEmail = r.FriendEmail,
                FriendName = $"{r.FirstName} {r.LastName}",
                Status = status,
                ConnectedDate = r.RequestedAt
            });
        }

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

    // Send a connection invitation to another user by email.
    [HttpPost("invite")]
    public IActionResult Invite([FromBody] InviteConnectionDto dto)
    {
        var email = GetEmail();

        try
        {
            var requestId = Friendship.CreateFriendRequest(email, dto.Email);
            return CreatedAtAction(nameof(GetAll), new { }, new { requestId, status = "pending" });
        }
        catch (SqlException ex) when (ex.Number == 50000)
        {
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

    // Accepts a pending friend request and creates the friendship record.
    [HttpPost("{id}/accept")]
    public IActionResult Accept(int id)
    {
        var email = GetEmail();

        var updated = Friendship.UpdateFriendRequestStatus(id, email, "Accepted");
        if (updated == null || updated.Status != "Accepted")
            return NotFound(new { message = "Pending request not found" });

        var friendshipId = Friendship.Create(updated.RequesterEmail, updated.AddresseeEmail);

        return Ok(new { friendshipId, status = "accepted" });
    }

    // Marks a pending friend request as Rejected.
    [HttpPost("{id}/decline")]
    public IActionResult Decline(int id)
    {
        var email = GetEmail();

        var updated = Friendship.UpdateFriendRequestStatus(id, email, "Rejected");
        if (updated == null || updated.Status != "Rejected")
            return NotFound(new { message = "Pending request not found" });

        return NoContent();
    }

    // Deactivates an existing friendship, making it no longer active.
    [HttpDelete("{id}")]
    public IActionResult Remove(int id)
    {
        var email = GetEmail();

        var removed = Friendship.Deactivate(id, email);
        if (!removed)
            return NotFound();

        return NoContent();
    }
}
