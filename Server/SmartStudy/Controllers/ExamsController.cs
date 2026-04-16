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

    private static ExamDto ToDto(ExamWithCourse e) => new()
    {
        ExamId = e.ExamId,
        CourseId = e.CourseId,
        CourseName = e.CourseName,
        Date = e.Date,
        Time = e.Time,
        Session = e.Session,
        Duration = e.Duration,
        DaysUntil = (int)(e.Date - DateTime.Today).TotalDays,
        IsTakingExam = e.IsTakingExam,
        ExamPrepHoursPerDay = e.ExamPrepHoursPerDay,
        ExamPrepDays = e.ExamPrepDays
    };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var exams = await _db.GetExamsByUserAsync(email);
        return Ok(exams.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();
        return Ok(ToDto(exam));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto)
    {
        var email = GetEmail();
        var isTakingExam = dto.Session != "B";
        var examId = await _db.CreateExamAsync(dto.CourseId, dto.Date, TimeSpan.Parse(dto.Time), dto.Session, dto.Duration, isTakingExam);

        // Prep settings live on the Course (single shared value, propagates to
        // every exam of that course). Only write through when the form sent
        // a value — null means "leave course default alone".
        if (dto.ExamPrepHoursPerDay.HasValue || dto.ExamPrepDays.HasValue)
        {
            await _db.UpdateCourseAsync(dto.CourseId,
                examPrepHoursPerDay: dto.ExamPrepHoursPerDay,
                examPrepDays: dto.ExamPrepDays);
        }

        await _scheduling.ScheduleAllTasksAsync(email);

        var created = await _db.GetExamByIdAsync(examId, email);
        if (created == null) return NotFound();
        return CreatedAtAction(nameof(Get), new { id = examId }, ToDto(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExamDto dto)
    {
        var email = GetEmail();
        var exam = await _db.GetExamByIdAsync(id, email);
        if (exam == null) return NotFound();

        TimeSpan? time = dto.Time != null ? TimeSpan.Parse(dto.Time) : null;
        await _db.UpdateExamAsync(id, dto.CourseId, dto.Date, time, dto.Session, dto.Duration);

        // See Create — prep fields propagate to the course (and therefore every
        // exam of that course) rather than being stored per-exam.
        if (dto.ExamPrepHoursPerDay.HasValue || dto.ExamPrepDays.HasValue)
        {
            var courseId = dto.CourseId ?? exam.CourseId;
            await _db.UpdateCourseAsync(courseId,
                examPrepHoursPerDay: dto.ExamPrepHoursPerDay,
                examPrepDays: dto.ExamPrepDays);
        }

        await _scheduling.ScheduleAllTasksAsync(email);

        var updated = await _db.GetExamByIdAsync(id, email);
        return Ok(updated == null ? ToDto(exam) : ToDto(updated));
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

        exam.IsTakingExam = newIsTaking;
        return Ok(ToDto(exam));
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
