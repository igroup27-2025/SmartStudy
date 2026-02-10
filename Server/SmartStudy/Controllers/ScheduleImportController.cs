using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/schedule")]
[Authorize]
public class ScheduleImportController : ControllerBase
{
    private readonly ScheduleImportService _importService;

    public ScheduleImportController(ScheduleImportService importService)
        => _importService = importService;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpPost("import")]
    public async Task<IActionResult> ImportSchedule(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are accepted" });

        if (file.Length > 10 * 1024 * 1024) // 10MB limit
            return BadRequest(new { message = "File too large (max 10MB)" });

        var email = GetEmail();

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportScheduleAsync(stream, email);
        return Ok(result);
    }
}
