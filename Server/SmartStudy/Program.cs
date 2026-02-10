using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartStudy.Data;
using SmartStudy.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - SQL Server
var connectionString = builder.Configuration.GetConnectionString("SmartStudyDb");
builder.Services.AddDbContext<SmartStudyDbContext>(options =>
    options.UseSqlServer(connectionString));

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

// Create tables and seed data on first run (works on shared school DB)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartStudyDbContext>();

    // Check if tables need to be created (works on shared school DB)
    var conn = db.Database.GetDbConnection();
    conn.Open();
    bool needsSetup = false;
    using (var checkCmd = conn.CreateCommand())
    {
        // Check if SmartStudy_Courses exists and has data (courses are the core entity)
        checkCmd.CommandText = @"
            IF OBJECT_ID('SmartStudy_Courses','U') IS NULL SELECT 0
            ELSE SELECT COUNT(*) FROM SmartStudy_Courses";
        var result = Convert.ToInt32(checkCmd.ExecuteScalar());
        needsSetup = (result == 0);
    }

    if (needsSetup)
    {
        // Drop all SmartStudy tables and recreate with correct schema
        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = @"
            IF OBJECT_ID('SmartStudy_StudyConnections','U') IS NOT NULL DROP TABLE SmartStudy_StudyConnections;
            IF OBJECT_ID('SmartStudy_TaskEvents','U') IS NOT NULL DROP TABLE SmartStudy_TaskEvents;
            IF OBJECT_ID('SmartStudy_ClassEvents','U') IS NOT NULL DROP TABLE SmartStudy_ClassEvents;
            IF OBJECT_ID('SmartStudy_WorkEvents','U') IS NOT NULL DROP TABLE SmartStudy_WorkEvents;
            IF OBJECT_ID('SmartStudy_PersonalEvents','U') IS NOT NULL DROP TABLE SmartStudy_PersonalEvents;
            IF OBJECT_ID('SmartStudy_Events','U') IS NOT NULL DROP TABLE SmartStudy_Events;
            IF OBJECT_ID('SmartStudy_Exams','U') IS NOT NULL DROP TABLE SmartStudy_Exams;
            IF OBJECT_ID('SmartStudy_Tasks','U') IS NOT NULL DROP TABLE SmartStudy_Tasks;
            IF OBJECT_ID('SmartStudy_UserCourses','U') IS NOT NULL DROP TABLE SmartStudy_UserCourses;
            IF OBJECT_ID('SmartStudy_Courses','U') IS NOT NULL DROP TABLE SmartStudy_Courses;
            IF OBJECT_ID('SmartStudy_NotificationSettings','U') IS NOT NULL DROP TABLE SmartStudy_NotificationSettings;
            IF OBJECT_ID('SmartStudy_Users','U') IS NOT NULL DROP TABLE SmartStudy_Users;
            IF OBJECT_ID('SmartStudy_Instructors','U') IS NOT NULL DROP TABLE SmartStudy_Instructors;";
        dropCmd.ExecuteNonQuery();

        var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        creator.CreateTables();
    }
    conn.Close();

    SeedDataService.SeedDatabase(db);

    // Drop unique index on TaskEvents.TaskId if it exists (allow 1:N task splitting)
    conn.Open();
    using (var fixCmd = conn.CreateCommand())
    {
        fixCmd.CommandText = @"
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartStudy_TaskEvents_TaskId' AND object_id = OBJECT_ID('SmartStudy_TaskEvents'))
            BEGIN
                DROP INDEX IX_SmartStudy_TaskEvents_TaskId ON SmartStudy_TaskEvents;
                CREATE NONCLUSTERED INDEX IX_SmartStudy_TaskEvents_TaskId ON SmartStudy_TaskEvents(TaskId);
            END";
        fixCmd.ExecuteNonQuery();
    }
    conn.Close();
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
