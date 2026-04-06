using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly DBservices _db;
    private readonly SchedulingService _scheduling;

    public ExamsController(DBservices db, SchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var exams = await _db.GetExamsByUserAsync(email);

        return Ok(exams.Select(e => new ExamDto
        {
            ExamId = e.ExamId,
            CourseId = e.CourseId,
            CourseName = e.CourseName,
            Date = e.Date,
            Time = e.Time,
            Session = e.Session,
            Duration = e.Duration,
            DaysUntil = (int)(e.Date - DateTime.Today).TotalDays,
            IsTakingExam = e.IsTakingExam
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();

        return Ok(new ExamDto
        {
            ExamId = exam.ExamId,
            CourseId = exam.CourseId,
            CourseName = exam.CourseName,
            Date = exam.Date,
            Time = exam.Time,
            Session = exam.Session,
            Duration = exam.Duration,
            DaysUntil = (int)(exam.Date - DateTime.Today).TotalDays,
            IsTakingExam = exam.IsTakingExam
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto)
    {
        var email = GetEmail();
        var isTakingExam = dto.Session != "B";
        var examId = await _db.CreateExamAsync(dto.CourseId, dto.Date, TimeSpan.Parse(dto.Time), dto.Session, dto.Duration, isTakingExam);

        await _scheduling.ScheduleAllTasksAsync(email);

        var course = await _db.GetCourseByIdAsync(dto.CourseId);
        return CreatedAtAction(nameof(Get), new { id = examId }, new ExamDto
        {
            ExamId = examId,
            CourseId = dto.CourseId,
            CourseName = course?.CourseName ?? "",
            Date = dto.Date,
            Time = TimeSpan.Parse(dto.Time),
            Session = dto.Session,
            Duration = dto.Duration,
            DaysUntil = (int)(dto.Date - DateTime.Today).TotalDays,
            IsTakingExam = isTakingExam
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExamDto dto)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();

        TimeSpan? time = dto.Time != null ? TimeSpan.Parse(dto.Time) : null;
        await _db.UpdateExamAsync(id, dto.CourseId, dto.Date, time, dto.Session, dto.Duration);

        await _scheduling.ScheduleAllTasksAsync(email);

        // Re-fetch updated exam
        var updated = await _db.GetExamByIdAsync(id, email);
        return Ok(new ExamDto
        {
            ExamId = id,
            CourseId = updated?.CourseId ?? exam.CourseId,
            CourseName = updated?.CourseName ?? exam.CourseName,
            Date = updated?.Date ?? exam.Date,
            Time = updated?.Time ?? exam.Time,
            Session = updated?.Session ?? exam.Session,
            Duration = updated?.Duration ?? exam.Duration,
            DaysUntil = (int)((updated?.Date ?? exam.Date) - DateTime.Today).TotalDays,
            IsTakingExam = updated?.IsTakingExam ?? exam.IsTakingExam
        });
    }

    [HttpPut("{id}/toggle-taking")]
    public async Task<IActionResult> ToggleTaking(int id)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();

        await _db.ToggleExamTakingAsync(id);
        var newIsTaking = !exam.IsTakingExam;

        if (!newIsTaking)
        {
            await _db.DeleteStudyTasksForExamAsync(email, exam.CourseId, exam.Date);
        }

        await _scheduling.ScheduleAllTasksAsync(email);

        return Ok(new ExamDto
        {
            ExamId = exam.ExamId,
            CourseId = exam.CourseId,
            CourseName = exam.CourseName,
            Date = exam.Date,
            Time = exam.Time,
            Session = exam.Session,
            Duration = exam.Duration,
            DaysUntil = (int)(exam.Date - DateTime.Today).TotalDays,
            IsTakingExam = newIsTaking
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();

        await _db.DeleteExamAsync(id);
        await _scheduling.ScheduleAllTasksAsync(email);

        return NoContent();
    }
}
