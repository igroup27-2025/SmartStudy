using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

// API endpoints for course CRUD, study-partner pairing, and per-course settings.
[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns all courses the user is enrolled in, with task/exam counts and partner names.
    [HttpGet]
    public IActionResult GetAll()
    {
        var email = GetEmail();
        var courses = Course.GetByUser(email);

        // Resolve partner names
        var partnerEmails = courses.Where(c => c.StudyPartnerEmail != null).Select(c => c.StudyPartnerEmail!).Distinct().ToList();
        var partners = new Dictionary<string, string>();
        foreach (var pe in partnerEmails)
        {
            var u = UserModel.GetByEmail(pe);
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

    // Returns one course by ID if the user is enrolled in it.
    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var email = GetEmail();
        if (!Course.UserCourseExists(email, id)) return NotFound();

        var course = Course.GetById(id);
        if (course == null) return NotFound();

        // Get counts from the user-specific query
        var userCourses = Course.GetByUser(email);
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

    // Creates a new course and enrolls the current user in it.
    [HttpPost]
    public IActionResult Create([FromBody] CreateCourseDto dto)
    {
        var email = GetEmail();
        var maxId = Course.GetMaxCourseId();
        var courseId = maxId + 1;

        Course.Create(courseId, dto.CourseName, dto.WeeklyHours, dto.Credits, dto.Semester, dto.InstructorId);
        Course.CreateUserCourse(email, courseId);

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

    // Updates course fields and re-runs auto-scheduling if estimation settings changed.
    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] UpdateCourseDto dto)
    {
        var email = GetEmail();
        if (!Course.UserCourseExists(email, id)) return NotFound();

        Course.Update(id, dto.CourseName, dto.WeeklyHours, dto.Credits, dto.Semester,
            dto.InstructorId, dto.DefaultTaskEstimatedHours, dto.ExamPrepHoursPerDay, dto.ExamPrepDays);

        if (dto.SharedByDefault.HasValue)
            Course.UpdateSharedByDefault(email, id, dto.SharedByDefault.Value);

        if (dto.DefaultTaskEstimatedHours.HasValue || dto.ExamPrepHoursPerDay.HasValue
            || dto.ExamPrepDays.HasValue || dto.Credits.HasValue)
        {
            StudentTask.ScheduleAll(email);
        }

        var course = Course.GetById(id);
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

    // Sets or clears a study partner for the course (must be a friend) and notifies them.
    [HttpPut("{id}/partner")]
    public IActionResult SetStudyPartner(int id, [FromBody] SetStudyPartnerDto dto)
    {
        var email = GetEmail();
        if (!Course.UserCourseExists(email, id)) return NotFound();

        if (!string.IsNullOrEmpty(dto.Email))
        {
            if (!Friendship.ExistsBetween(email, dto.Email))
                return BadRequest(new { message = "You must be friends to set as study partner" });
        }

        var partnerEmail = string.IsNullOrEmpty(dto.Email) ? null : dto.Email;
        Course.UpdateStudyPartner(email, id, partnerEmail);

        if (!string.IsNullOrEmpty(dto.Email))
        {
            var sender = UserModel.GetByEmail(email);
            var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : email;
            var course = Course.GetById(id);
            var courseName = course?.CourseName ?? "a course";

            Notification.Create(dto.Email, "study_partner", "Study Partner Invitation",
                $"{senderName} set you as a study partner for \"{courseName}\". Tasks may be shared automatically.",
                id, "Course");
        }

        return Ok(new { courseId = id, studyPartnerEmail = partnerEmail });
    }

    // Removes the user's enrollment in the course (unenroll, not global delete).
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var email = GetEmail();
        if (!Course.UserCourseExists(email, id)) return NotFound();

        Course.DeleteUserCourse(email, id);
        return NoContent();
    }
}
