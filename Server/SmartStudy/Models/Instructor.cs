using SmartStudy.DAL;

namespace SmartStudy.Models;

// Course instructor lookup record (global, shared across users).
public class Instructor
{
    public int InstructorId { get; set; }
    public string InstructorName { get; set; } = null!;

    // Navigation property
    public ICollection<Course> Courses { get; set; } = new List<Course>();

    // Returns every instructor in the database (used for course-creation dropdowns).
    public static List<Instructor> GetAll()
    {
        DBservices db = new DBservices();
        return db.GetAllInstructors();
    }
}
