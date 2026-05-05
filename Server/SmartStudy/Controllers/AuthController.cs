using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.Models;
using SmartStudy.Services;
using UserModel = SmartStudy.Models.User;
using NotifSettingsModel = SmartStudy.Models.NotificationSettings;

namespace SmartStudy.Controllers;

// API endpoints for user login, registration, password reset, and Google sign-in.
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly EmailService _emailService;
    private readonly RuppinetSyncService _ruppinetSync;
    private readonly ILogger<AuthController> _logger;

    // Injects configuration, email service, Ruppinet sync, and logger.
    public AuthController(IConfiguration config, EmailService emailService,
        RuppinetSyncService ruppinetSync, ILogger<AuthController> logger)
    {
        _config = config;
        _emailService = emailService;
        _ruppinetSync = ruppinetSync;
        _logger = logger;
    }

    // Authenticates by email/password, issues JWT, and triggers Ruppinet background sync.
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        var user = UserModel.GetByEmail(dto.Email);
        if (user == null || user.Password != HashPassword(dto.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        var token = GenerateToken(user);

        // Trigger Ruppinet sync in background (non-blocking)
        RuppinetSyncResultDto? syncResult = null;
        var syncIntervalHours = int.TryParse(_config["Ruppinet:SyncIntervalHours"], out var h) ? h : 12;
        if (!string.IsNullOrEmpty(user.RuppinetId) &&
            (user.LastRuppinetSync == null || user.LastRuppinetSync < DateTime.UtcNow.AddHours(-syncIntervalHours)))
        {
            try { syncResult = _ruppinetSync.SyncAllAsync(user.Email).GetAwaiter().GetResult(); }
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

    // Creates a new account with default notification settings and returns a JWT.
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        if (UserModel.Exists(dto.Email))
            return BadRequest(new { message = "Email already registered" });

        var hashedPassword = HashPassword(dto.Password);
        UserModel.Create(dto.Email, dto.FirstName, dto.LastName, hashedPassword);
        NotifSettingsModel.CreateDefault(dto.Email);

        var user = new UserModel
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

    // Stateless logout endpoint — JWT is discarded client-side.
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logged out successfully" });
    }

    // Generates a one-hour reset token and emails it to the user (does not reveal account existence).
    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = UserModel.GetByEmail(dto.Email);
        if (user == null)
            return Ok(new { message = "If the email exists, a reset link has been sent." });

        var token = Guid.NewGuid().ToString("N")[..8].ToUpper();
        UserModel.UpdateResetToken(dto.Email, token, DateTime.UtcNow.AddHours(1));

        _emailService.SendAsync(
            dto.Email,
            "SmartStudy — Password Reset",
            $"Your password reset code: {token}\n\nThis code expires in 1 hour.\n\nIf you did not request a password reset, please ignore this email."
        ).GetAwaiter().GetResult();

        if (!_emailService.IsConfigured)
            return Ok(new { message = "If the email exists, a reset link has been sent.", resetToken = token });

        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    // Validates the reset token and updates the user's password hash.
    [HttpPost("reset-password")]
    public IActionResult ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = UserModel.GetByEmail(dto.Email);
        if (user == null)
            return BadRequest(new { message = "Invalid email or token" });

        if (user.ResetToken != dto.Token || user.ResetTokenExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired token" });

        UserModel.ResetPassword(dto.Email, HashPassword(dto.NewPassword));

        return Ok(new { message = "Password reset successfully" });
    }

    // Validates a Google ID token, auto-creates the user if new, and returns a JWT.
    [HttpPost("google")]
    public IActionResult GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        try
        {
            var payload = Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] ?? "" }
            }).GetAwaiter().GetResult();

            var email = payload.Email;
            var user = UserModel.GetByEmail(email);
            var isNewUser = false;

            if (user == null)
            {
                isNewUser = true;
                var firstName = payload.GivenName ?? "User";
                var lastName = payload.FamilyName ?? "";
                UserModel.Create(email, firstName, lastName, HashPassword(Guid.NewGuid().ToString()), "Google");
                NotifSettingsModel.CreateDefault(email);

                user = new UserModel
                {
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    OnboardingCompleted = false
                };
            }
            else if (!user.OnboardingCompleted)
            {
                UserModel.SetOnboardingComplete(user.Email);
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

    // Returns public client config (Google OAuth client ID) for the frontend.
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new AuthConfigDto
        {
            GoogleClientId = _config["Google:ClientId"]
        });
    }

    // Builds a 7-day HS256-signed JWT containing the user's email and full name.
    private string GenerateToken(UserModel user)
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

    // SHA-256 password hash with a static salt suffix.
    private static string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "SmartStudySalt2026"));
        return Convert.ToBase64String(bytes);
    }
}
