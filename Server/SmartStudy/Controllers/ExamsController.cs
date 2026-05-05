using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
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
    public IActionResult GetAll()
    {
        var email = GetEmail();
        var exams = Exam.GetByUser(email);
        return Ok(exams.Select(ToDto));
    }

    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var email = GetEmail();
        var exam = Exam.GetById(id, email);
        if (exam == null) return NotFound();
        return Ok(ToDto(exam));
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateExamDto dto)
    {
        var email = GetEmail();
        var isTakingExam = dto.Session != "B";
        var examId = Exam.Create(dto.CourseId, dto.Date, TimeSpan.Parse(dto.Time), dto.Session, dto.Duration, isTakingExam);

        if (dto.ExamPrepHoursPerDay.HasValue || dto.ExamPrepDays.HasValue)
        {
            Course.Update(dto.CourseId,
                examPrepHoursPerDay: dto.ExamPrepHoursPerDay,
                examPrepDays: dto.ExamPrepDays);
        }

        StudentTask.ScheduleAll(email);

        var created = Exam.GetById(examId, email);
        if (created == null) return NotFound();
        return CreatedAtAction(nameof(Get), new { id = examId }, ToDto(created));
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] UpdateExamDto dto)
    {
        var email = GetEmail();
        var exam = Exam.GetById(id, email);
        if (exam == null) return NotFound();

        TimeSpan? time = dto.Time != null ? TimeSpan.Parse(dto.Time) : null;
        Exam.Update(id, dto.CourseId, dto.Date, time, dto.Session, dto.Duration);

        if (dto.ExamPrepHoursPerDay.HasValue || dto.ExamPrepDays.HasValue)
        {
            var courseId = dto.CourseId ?? exam.CourseId;
            Course.Update(courseId,
                examPrepHoursPerDay: dto.ExamPrepHoursPerDay,
                examPrepDays: dto.ExamPrepDays);
        }

        StudentTask.ScheduleAll(email);

        var updated = Exam.GetById(id, email);
        return Ok(updated == null ? ToDto(exam) : ToDto(updated));
    }

    [HttpPut("{id}/toggle-taking")]
    public IActionResult ToggleTaking(int id)
    {
        var email = GetEmail();
        var exam = Exam.GetById(id, email);
        if (exam == null) return NotFound();

        Exam.ToggleTaking(id);
        var newIsTaking = !exam.IsTakingExam;

        if (!newIsTaking)
        {
            Exam.DeleteStudyTasksForExam(email, exam.CourseId, exam.Date);
        }

        StudentTask.ScheduleAll(email);

        exam.IsTakingExam = newIsTaking;
        return Ok(ToDto(exam));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var email = GetEmail();
        var exam = Exam.GetById(id, email);
        if (exam == null) return NotFound();

        Exam.Delete(id);
        StudentTask.ScheduleAll(email);

        return NoContent();
    }
}
