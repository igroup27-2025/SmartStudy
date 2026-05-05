using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/stress")]
[Authorize]
public class StressController : ControllerBase
{
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet("score")]
    public IActionResult GetScore()
    {
        var score = UserModel.GetStressScore(GetEmail());
        return Ok(score);
    }

    [HttpGet("weekly")]
    public IActionResult GetWeekly()
    {
        var weekly = UserModel.GetWeeklyStress(GetEmail());
        return Ok(weekly);
    }
}
