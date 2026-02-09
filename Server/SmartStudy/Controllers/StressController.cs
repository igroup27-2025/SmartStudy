using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/stress")]
[Authorize]
public class StressController : ControllerBase
{
    private readonly StressService _stressService;

    public StressController(StressService stressService) => _stressService = stressService;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet("score")]
    public async Task<IActionResult> GetScore()
    {
        var score = await _stressService.GetStressScoreAsync(GetEmail());
        return Ok(score);
    }

    [HttpGet("weekly")]
    public async Task<IActionResult> GetWeekly()
    {
        var weekly = await _stressService.GetWeeklyStressAsync(GetEmail());
        return Ok(weekly);
    }
}
