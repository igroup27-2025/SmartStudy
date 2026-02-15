using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly SmartStudyDbContext _db;

    public CoursesController(SmartStudyDbContext db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var userCourses = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .ToListAsync();
        var courseIds = userCourses.Select(uc => uc.CourseId).ToList();

        var courses = await _db.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Tasks)
            .Include(c => c.Exams)
            .Where(c => courseIds.Contains(c.CourseId))
            .ToListAsync();

        // Resolve partner names
        var partnerEmails = userCourses.Where(uc => uc.StudyPartnerEmail != null).Select(uc => uc.StudyPartnerEmail!).Distinct().ToList();
        var partners = await _db.Users.Where(u => partnerEmails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email, u => $"{u.FirstName} {u.LastName}");

        var result = courses.Select(c =>
        {
            var uc = userCourses.FirstOrDefault(x => x.CourseId == c.CourseId);
            return new CourseDto
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                WeeklyHours = c.WeeklyHours,
                Credits = c.Credits,
                Semester = c.Semester,
                InstructorId = c.InstructorId,
                InstructorName = c.Instructor?.InstructorName,
                TaskCount = c.Tasks.Count(t => t.Email == email),
                ExamCount = c.Exams.Count,
                StudyPartnerEmail = uc?.StudyPartnerEmail,
                StudyPartnerName = uc?.StudyPartnerEmail != null && partners.ContainsKey(uc.StudyPartnerEmail) ? partners[uc.StudyPartnerEmail] : null,
                SharedByDefault = uc?.SharedByDefault ?? false
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var enrolled = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == id);
        if (!enrolled) return NotFound();

        var course = await _db.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Tasks)
            .Include(c => c.Exams)
            .FirstOrDefaultAsync(c => c.CourseId == id);

        if (course == null) return NotFound();

        return Ok(new CourseDto
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            WeeklyHours = course.WeeklyHours,
            Credits = course.Credits,
            Semester = course.Semester,
            InstructorId = course.InstructorId,
            InstructorName = course.Instructor?.InstructorName,
            TaskCount = course.Tasks.Count(t => t.Email == email),
            ExamCount = course.Exams.Count
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseDto dto)
    {
        var email = GetEmail();
        var maxId = await _db.Courses.AnyAsync() ? await _db.Courses.MaxAsync(c => c.CourseId) : 0;

        var course = new Course
        {
            CourseId = maxId + 1,
            CourseName = dto.CourseName,
            WeeklyHours = dto.WeeklyHours,
            Credits = dto.Credits,
            Semester = dto.Semester,
            InstructorId = dto.InstructorId
        };
        _db.Courses.Add(course);
        _db.UserCourses.Add(new UserCourse { Email = email, CourseId = course.CourseId });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = course.CourseId }, new CourseDto
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            WeeklyHours = course.WeeklyHours,
            Credits = course.Credits,
            Semester = course.Semester,
            InstructorId = course.InstructorId,
            TaskCount = 0,
            ExamCount = 0
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCourseDto dto)
    {
        var email = GetEmail();
        var enrolled = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == id);
        if (!enrolled) return NotFound();

        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        if (dto.CourseName != null) course.CourseName = dto.CourseName;
        if (dto.WeeklyHours.HasValue) course.WeeklyHours = dto.WeeklyHours;
        if (dto.Credits.HasValue) course.Credits = dto.Credits;
        if (dto.Semester != null) course.Semester = dto.Semester;
        if (dto.InstructorId.HasValue) course.InstructorId = dto.InstructorId;

        // Handle SharedByDefault on the UserCourse junction
        if (dto.SharedByDefault.HasValue)
        {
            var uc = await _db.UserCourses.FirstOrDefaultAsync(x => x.Email == email && x.CourseId == id);
            if (uc != null) uc.SharedByDefault = dto.SharedByDefault.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(new CourseDto
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            WeeklyHours = course.WeeklyHours,
            Credits = course.Credits,
            Semester = course.Semester,
            InstructorId = course.InstructorId,
            SharedByDefault = dto.SharedByDefault ?? false
        });
    }

    [HttpPut("{id}/partner")]
    public async Task<IActionResult> SetStudyPartner(int id, [FromBody] SetStudyPartnerDto dto)
    {
        var email = GetEmail();
        var uc = await _db.UserCourses.FirstOrDefaultAsync(x => x.Email == email && x.CourseId == id);
        if (uc == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.Email))
        {
            // Validate friendship
            var (e1, e2) = NormalizePair(email, dto.Email);
            var isFriend = await _db.Friendships.AnyAsync(f => f.Email1 == e1 && f.Email2 == e2 && f.IsActive);
            if (!isFriend)
                return BadRequest(new { message = "You must be friends to set as study partner" });
        }

        uc.StudyPartnerEmail = string.IsNullOrEmpty(dto.Email) ? null : dto.Email;
        await _db.SaveChangesAsync();

        return Ok(new { courseId = id, studyPartnerEmail = uc.StudyPartnerEmail });
    }

    private static (string, string) NormalizePair(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? (a, b) : (b, a);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var enrollment = await _db.UserCourses.FirstOrDefaultAsync(uc => uc.Email == email && uc.CourseId == id);
        if (enrollment == null) return NotFound();

        _db.UserCourses.Remove(enrollment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
