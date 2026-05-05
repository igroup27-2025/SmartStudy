using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for exam CRUD with auto-prep scheduling.
[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Maps an exam-with-course row to the wire-format ExamDto.
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

    // Returns all exams for the user's enrolled courses.
    [HttpGet]
    public IActionResult GetAll()
    {
        var email = GetEmail();
        var exams = Exam.GetByUser(email);
        return Ok(exams.Select(ToDto));
    }

    // Returns one exam by ID if it belongs to a course the user is enrolled in.
    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var email = GetEmail();
        var exam = Exam.GetById(id, email);
        if (exam == null) return NotFound();
        return Ok(ToDto(exam));
    }

    // Creates an exam, optionally updates course prep settings, and re-runs scheduling.
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

    // Updates an exam (and its course's prep config) and re-runs scheduling.
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

    // Toggles whether the user is taking the exam, removing prep tasks if opted out.
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

    // Deletes an exam and re-runs scheduling.
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
