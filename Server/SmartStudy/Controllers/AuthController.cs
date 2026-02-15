using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(SmartStudyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || user.Password != HashPassword(dto.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        var token = GenerateToken(user);
        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            OnboardingCompleted = user.OnboardingCompleted,
            IsNewUser = false
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { message = "Email already registered" });

        var user = new User
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Password = HashPassword(dto.Password)
        };
        _db.Users.Add(user);

        _db.NotificationSettings.Add(new NotificationSettings { Email = dto.Email });

        await _db.SaveChangesAsync();

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
        var user = await _db.Users.FindAsync(dto.Email);
        if (user == null)
            return Ok(new { message = "If the email exists, a reset token has been generated.", token = (string?)null });

        // Generate 8-char reset token
        var token = Guid.NewGuid().ToString("N")[..8].ToUpper();
        user.ResetToken = token;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        // In production this would be sent via email. For demo, return it directly.
        return Ok(new { message = "Reset token generated.", token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _db.Users.FindAsync(dto.Email);
        if (user == null)
            return BadRequest(new { message = "Invalid email or token" });

        if (user.ResetToken != dto.Token || user.ResetTokenExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired token" });

        user.Password = HashPassword(dto.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

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
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            var isNewUser = false;

            if (user == null)
            {
                isNewUser = true;
                // Create new user from Google profile
                user = new User
                {
                    Email = email,
                    FirstName = payload.GivenName ?? "User",
                    LastName = payload.FamilyName ?? "",
                    Password = HashPassword(Guid.NewGuid().ToString()),
                    AuthProvider = "Google",
                    OnboardingCompleted = false
                };
                _db.Users.Add(user);
                _db.NotificationSettings.Add(new NotificationSettings { Email = email });
                await _db.SaveChangesAsync();
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
