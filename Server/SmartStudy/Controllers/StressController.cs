using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

// API endpoints for the workload-based stress score.
[ApiController]
[Route("api/stress")]
[Authorize]
public class StressController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns the user's current stress score (0-100) and zone.
    [HttpGet("score")]
    public IActionResult GetScore()
    {
        var score = UserModel.GetStressScore(GetEmail());
        return Ok(score);
    }

    // Returns the user's per-day stress score for the current week.
    [HttpGet("weekly")]
    public IActionResult GetWeekly()
    {
        var weekly = UserModel.GetWeeklyStress(GetEmail());
        return Ok(weekly);
    }
}
