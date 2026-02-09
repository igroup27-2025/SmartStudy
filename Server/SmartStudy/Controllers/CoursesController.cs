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
        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var courses = await _db.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Tasks)
            .Include(c => c.Exams)
            .Where(c => courseIds.Contains(c.CourseId))
            .Select(c => new CourseDto
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                WeeklyHours = c.WeeklyHours,
                Credits = c.Credits,
                Semester = c.Semester,
                InstructorId = c.InstructorId,
                InstructorName = c.Instructor != null ? c.Instructor.InstructorName : null,
                TaskCount = c.Tasks.Count(t => t.Email == email),
                ExamCount = c.Exams.Count
            })
            .ToListAsync();

        return Ok(courses);
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

        await _db.SaveChangesAsync();
        return Ok(new CourseDto
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            WeeklyHours = course.WeeklyHours,
            Credits = course.Credits,
            Semester = course.Semester,
            InstructorId = course.InstructorId
        });
    }

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
