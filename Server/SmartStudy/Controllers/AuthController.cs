using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DBservices _db;
    private readonly IConfiguration _config;
    private readonly EmailService _emailService;
    private readonly RuppinetSyncService _ruppinetSync;
    private readonly ILogger<AuthController> _logger;

    public AuthController(DBservices db, IConfiguration config, EmailService emailService,
        RuppinetSyncService ruppinetSync, ILogger<AuthController> logger)
    {
        _db = db;
        _config = config;
        _emailService = emailService;
        _ruppinetSync = ruppinetSync;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.GetUserByEmailAsync(dto.Email);
        if (user == null || user.Password != HashPassword(dto.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        var token = GenerateToken(user);

        // Trigger Ruppinet sync in background (non-blocking)
        DTOs.RuppinetSyncResultDto? syncResult = null;
        var syncIntervalHours = int.TryParse(_config["Ruppinet:SyncIntervalHours"], out var h) ? h : 12;
        if (!string.IsNullOrEmpty(user.RuppinetId) &&
            (user.LastRuppinetSync == null || user.LastRuppinetSync < DateTime.UtcNow.AddHours(-syncIntervalHours)))
        {
            try { syncResult = await _ruppinetSync.SyncAllAsync(user.Email); }
            catch (Exception ex) { _logger.LogWarning(ex, "Ruppinet sync failed during login for {Email}", user.Email); }
        }

        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            OnboardingCompleted = user.OnboardingCompleted,
            IsNewUser = false,
            RuppinetSynced = syncResult?.Success ?? false
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (await _db.UserExistsAsync(dto.Email))
            return BadRequest(new { message = "Email already registered" });

        var hashedPassword = HashPassword(dto.Password);
        await _db.CreateUserAsync(dto.Email, dto.FirstName, dto.LastName, hashedPassword);
        await _db.CreateDefaultNotifSettingsAsync(dto.Email);

        var user = new User
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };
        var token = GenerateToken(user);
        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            OnboardingCompleted = false,
            IsNewUser = true
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _db.GetUserByEmailAsync(dto.Email);
        if (user == null)
            return Ok(new { message = "If the email exists, a reset link has been sent." });

        // Generate 8-char reset token
        var token = Guid.NewGuid().ToString("N")[..8].ToUpper();
        await _db.UpdateResetTokenAsync(dto.Email, token, DateTime.UtcNow.AddHours(1));

        // Send reset email (falls back to console log if SMTP not configured)
        await _emailService.SendAsync(
            dto.Email,
            "SmartStudy — Password Reset",
            $"Your password reset code: {token}\n\nThis code expires in 1 hour.\n\nIf you did not request a password reset, please ignore this email."
        );

        // In dev/no-SMTP mode, return token so the frontend flow works
        if (!_emailService.IsConfigured)
            return Ok(new { message = "If the email exists, a reset link has been sent.", resetToken = token });

        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _db.GetUserByEmailAsync(dto.Email);
        if (user == null)
            return BadRequest(new { message = "Invalid email or token" });

        if (user.ResetToken != dto.Token || user.ResetTokenExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired token" });

        await _db.ResetPasswordAsync(dto.Email, HashPassword(dto.NewPassword));

        return Ok(new { message = "Password reset successfully" });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        try
        {
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] ?? "" }
            });

            var email = payload.Email;
            var user = await _db.GetUserByEmailAsync(email);
            var isNewUser = false;

            if (user == null)
            {
                isNewUser = true;
                var firstName = payload.GivenName ?? "User";
                var lastName = payload.FamilyName ?? "";
                await _db.CreateUserAsync(email, firstName, lastName, HashPassword(Guid.NewGuid().ToString()), "Google");
                await _db.CreateDefaultNotifSettingsAsync(email);

                user = new User
                {
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    OnboardingCompleted = false
                };
            }
            else if (!user.OnboardingCompleted)
            {
                await _db.SetOnboardingCompleteAsync(user.Email);
                user.OnboardingCompleted = true;
            }

            var token = GenerateToken(user);
            return Ok(new AuthResponseDto
            {
                Token = token,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                OnboardingCompleted = user.OnboardingCompleted,
                IsNewUser = isNewUser
            });
        }
        catch (Exception)
        {
            return BadRequest(new { message = "Invalid Google token" });
        }
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new AuthConfigDto
        {
            GoogleClientId = _config["Google:ClientId"]
        });
    }

    private string GenerateToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? "SmartStudySuperSecretKey2026ForJwtTokenGeneration!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "SmartStudySalt2026"));
        return Convert.ToBase64String(bytes);
    }
}
