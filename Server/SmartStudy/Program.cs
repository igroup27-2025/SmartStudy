using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.DAL;
using SmartStudy.Services;

var builder = WebApplication.CreateBuilder(args);

// ADO.NET DAL (stored procedures). The schema must already exist in the database —
// run Schema.sql once against the target SQL Server before starting the app.
builder.Services.AddSingleton<SqlHelper>();
builder.Services.AddScoped<DBservices>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SmartStudySuperSecretKey2026ForJwtTokenGeneration!";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<StressService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ScheduleImportService>();
builder.Services.AddScoped<SafeZoneService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WeeklySuggestionService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ComposioService>();
builder.Services.AddScoped<GoogleCalendarService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<RuppinetApiClient>();
builder.Services.AddScoped<RuppinetSyncService>();
builder.Services.AddScoped<MoodleApiClient>();
builder.Services.AddScoped<MoodleSyncService>();
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHostedService<RuppinetBackgroundSyncService>();
builder.Services.AddHostedService<MoodleBackgroundSyncService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// PathBase for IIS subpath deployment (e.g. /igroup27/test2/tar1)
var pathBase = builder.Configuration["PathBase"] ?? "";
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

// Schema is managed externally via Schema.sql — nothing to do at startup.
// To provision a fresh database, run Schema.sql against SQL Server before starting the app.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve frontend static files from Front/ directory (dev only, prod uses separate IIS app)
var frontPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Front"));
if (!Directory.Exists(frontPath))
    frontPath = Path.Combine(builder.Environment.ContentRootPath, "Front");

if (Directory.Exists(frontPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontPath),
        RequestPath = ""
    });

    app.MapGet("/", () => Results.Redirect("Pages/Login.html"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
