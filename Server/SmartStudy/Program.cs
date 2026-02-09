using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.Data;
using SmartStudy.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - SQLite for development
builder.Services.AddDbContext<SmartStudyDbContext>(options =>
    options.UseSqlite("Data Source=smartstudy.db"));

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

// Create database and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartStudyDbContext>();
    db.Database.EnsureCreated();
    SeedDataService.SeedDatabase(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve frontend static files from Front/ directory
var frontPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Front"));
if (Directory.Exists(frontPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontPath),
        RequestPath = ""
    });

    // Redirect root to login page
    app.MapGet("/", () => Results.Redirect("/Pages/Login.html"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
