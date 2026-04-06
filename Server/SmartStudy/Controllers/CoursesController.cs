using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly DBservices _db;
    private readonly SchedulingService _scheduling;
    private readonly NotificationService _notifications;

    public CoursesController(DBservices db, SchedulingService scheduling, NotificationService notifications)
    {
        _db = db;
        _scheduling = scheduling;
        _notifications = notifications;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var courses = await _db.GetCoursesByUserAsync(email);

        // Resolve partner names
        var partnerEmails = courses.Where(c => c.StudyPartnerEmail != null).Select(c => c.StudyPartnerEmail!).Distinct().ToList();
        var partners = new Dictionary<string, string>();
        foreach (var pe in partnerEmails)
        {
            var u = await _db.GetUserByEmailAsync(pe);
            if (u != null) partners[pe] = $"{u.FirstName} {u.LastName}";
        }

        var result = courses.Select(c => new CourseDto
        {
            CourseId = c.CourseId,
            CourseName = c.CourseName,
            WeeklyHours = c.WeeklyHours,
            Credits = c.Credits,
            Semester = c.Semester,
            InstructorId = c.InstructorId,
            InstructorName = c.InstructorName,
            TaskCount = c.TaskCount,
            ExamCount = c.ExamCount,
            StudyPartnerEmail = c.StudyPartnerEmail,
            StudyPartnerName = c.StudyPartnerEmail != null && partners.ContainsKey(c.StudyPartnerEmail) ? partners[c.StudyPartnerEmail] : null,
            SharedByDefault = c.SharedByDefault,
            DefaultTaskEstimatedHours = c.DefaultTaskEstimatedHours,
            ExamPrepHoursPerDay = c.ExamPrepHoursPerDay,
            ExamPrepDays = c.ExamPrepDays
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        if (!await _db.UserCourseExistsAsync(email, id)) return NotFound();

        var course = await _db.GetCourseByIdAsync(id);
        if (course == null) return NotFound();

        // Get counts from the user-specific query
        var userCourses = await _db.GetCoursesByUserAsync(email);
        var uc = userCourses.FirstOrDefault(c => c.CourseId == id);

        return Ok(new CourseDto
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            WeeklyHours = course.WeeklyHours,
            Credits = course.Credits,
            Semester = course.Semester,
            InstructorId = course.InstructorId,
            InstructorName = uc?.InstructorName,
            TaskCount = uc?.TaskCount ?? 0,
            ExamCount = uc?.ExamCount ?? 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseDto dto)
    {
        var email = GetEmail();
        var maxId = await _db.GetMaxCourseIdAsync();
        var courseId = maxId + 1;

        await _db.CreateCourseAsync(courseId, dto.CourseName, dto.WeeklyHours, dto.Credits, dto.Semester, dto.InstructorId);
        await _db.CreateUserCourseAsync(email, courseId);

        return CreatedAtAction(nameof(Get), new { id = courseId }, new CourseDto
        {
            CourseId = courseId,
            CourseName = dto.CourseName,
            WeeklyHours = dto.WeeklyHours,
            Credits = dto.Credits,
            Semester = dto.Semester,
            InstructorId = dto.InstructorId,
            TaskCount = 0,
            ExamCount = 0
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCourseDto dto)
    {
        var email = GetEmail();
        if (!await _db.UserCourseExistsAsync(email, id)) return NotFound();

        await _db.UpdateCourseAsync(id, dto.CourseName, dto.WeeklyHours, dto.Credits, dto.Semester,
            dto.InstructorId, dto.DefaultTaskEstimatedHours, dto.ExamPrepHoursPerDay, dto.ExamPrepDays);

        if (dto.SharedByDefault.HasValue)
            await _db.UpdateSharedByDefaultAsync(email, id, dto.SharedByDefault.Value);

        // Reschedule if scheduling-relevant fields changed
        if (dto.DefaultTaskEstimatedHours.HasValue || dto.ExamPrepHoursPerDay.HasValue
            || dto.ExamPrepDays.HasValue || dto.Credits.HasValue)
        {
            await _scheduling.ScheduleAllTasksAsync(email);
        }

        var course = await _db.GetCourseByIdAsync(id);
        return Ok(new CourseDto
        {
            CourseId = id,
            CourseName = course?.CourseName ?? dto.CourseName ?? "",
            WeeklyHours = course?.WeeklyHours ?? dto.WeeklyHours,
            Credits = course?.Credits ?? dto.Credits,
            Semester = course?.Semester ?? dto.Semester,
            InstructorId = course?.InstructorId ?? dto.InstructorId,
            SharedByDefault = dto.SharedByDefault ?? false,
            DefaultTaskEstimatedHours = course?.DefaultTaskEstimatedHours,
            ExamPrepHoursPerDay = course?.ExamPrepHoursPerDay,
            ExamPrepDays = course?.ExamPrepDays
        });
    }

    [HttpPut("{id}/partner")]
    public async Task<IActionResult> SetStudyPartner(int id, [FromBody] SetStudyPartnerDto dto)
    {
        var email = GetEmail();
        if (!await _db.UserCourseExistsAsync(email, id)) return NotFound();

        if (!string.IsNullOrEmpty(dto.Email))
        {
            if (!await _db.FriendshipExistsAsync(email, dto.Email))
                return BadRequest(new { message = "You must be friends to set as study partner" });
        }

        var partnerEmail = string.IsNullOrEmpty(dto.Email) ? null : dto.Email;
        await _db.UpdateStudyPartnerAsync(email, id, partnerEmail);

        // Notify the study partner
        if (!string.IsNullOrEmpty(dto.Email))
        {
            var sender = await _db.GetUserByEmailAsync(email);
            var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : email;
            var course = await _db.GetCourseByIdAsync(id);
            var courseName = course?.CourseName ?? "a course";

            await _db.CreateNotificationAsync(dto.Email, "study_partner", "Study Partner Invitation",
                $"{senderName} set you as a study partner for \"{courseName}\". Tasks may be shared automatically.",
                id, "Course");
        }

        return Ok(new { courseId = id, studyPartnerEmail = partnerEmail });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        if (!await _db.UserCourseExistsAsync(email, id)) return NotFound();

        await _db.DeleteUserCourseAsync(email, id);
        return NoContent();
    }
}
