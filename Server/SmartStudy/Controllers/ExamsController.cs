using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly SchedulingService _scheduling;

    public ExamsController(SmartStudyDbContext db, SchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var exams = await _db.Exams
            .Include(e => e.Course)
            .Where(e => courseIds.Contains(e.CourseId))
            .OrderBy(e => e.Date)
            .Select(e => new ExamDto
            {
                ExamId = e.ExamId,
                CourseId = e.CourseId,
                CourseName = e.Course.CourseName,
                Date = e.Date,
                Time = e.Time,
                Session = e.Session,
                Duration = e.Duration,
                DaysUntil = (int)(e.Date - DateTime.Today).TotalDays
            })
            .ToListAsync();

        return Ok(exams);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var exam = await _db.Exams.Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.ExamId == id && courseIds.Contains(e.CourseId));

        if (exam == null) return NotFound();

        return Ok(new ExamDto
        {
            ExamId = exam.ExamId,
            CourseId = exam.CourseId,
            CourseName = exam.Course.CourseName,
            Date = exam.Date,
            Time = exam.Time,
            Session = exam.Session,
            Duration = exam.Duration,
            DaysUntil = (int)(exam.Date - DateTime.Today).TotalDays
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto)
    {
        var exam = new Exam
        {
            CourseId = dto.CourseId,
            Date = dto.Date,
            Time = TimeSpan.Parse(dto.Time),
            Session = dto.Session,
            Duration = dto.Duration
        };

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        // Trigger reschedule
        var email = GetEmail();
        await _scheduling.ScheduleAllTasksAsync(email);

        var course = await _db.Courses.FindAsync(dto.CourseId);
        return CreatedAtAction(nameof(Get), new { id = exam.ExamId }, new ExamDto
        {
            ExamId = exam.ExamId,
            CourseId = exam.CourseId,
            CourseName = course?.CourseName ?? "",
            Date = exam.Date,
            Time = exam.Time,
            Session = exam.Session,
            Duration = exam.Duration,
            DaysUntil = (int)(exam.Date - DateTime.Today).TotalDays
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExamDto dto)
    {
        var email = GetEmail();
        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var exam = await _db.Exams.Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.ExamId == id && courseIds.Contains(e.CourseId));

        if (exam == null) return NotFound();

        if (dto.CourseId.HasValue) exam.CourseId = dto.CourseId.Value;
        if (dto.Date.HasValue) exam.Date = dto.Date.Value;
        if (dto.Time != null) exam.Time = TimeSpan.Parse(dto.Time);
        if (dto.Session != null) exam.Session = dto.Session;
        if (dto.Duration.HasValue) exam.Duration = dto.Duration;

        await _db.SaveChangesAsync();

        // Trigger reschedule
        await _scheduling.ScheduleAllTasksAsync(email);

        return Ok(new ExamDto
        {
            ExamId = exam.ExamId,
            CourseId = exam.CourseId,
            CourseName = exam.Course.CourseName,
            Date = exam.Date,
            Time = exam.Time,
            Session = exam.Session,
            Duration = exam.Duration,
            DaysUntil = (int)(exam.Date - DateTime.Today).TotalDays
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.ExamId == id && courseIds.Contains(e.CourseId));
        if (exam == null) return NotFound();

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync();

        // Trigger reschedule
        await _scheduling.ScheduleAllTasksAsync(email);

        return NoContent();
    }
}
