using SmartStudy.Data;
using SmartStudy.Models;

namespace SmartStudy.Services;

public static class SeedDataService
{
    public static void SeedDatabase(SmartStudyDbContext db)
    {
        if (db.Users.Any()) return;

        // === Users ===
        var user1 = new User
        {
            Email = "demo@smartstudy.com",
            FirstName = "Or",
            LastName = "Cohen",
            Password = BCryptSimple("Demo123")
        };
        var user2 = new User
        {
            Email = "yuval@smartstudy.com",
            FirstName = "Yuval",
            LastName = "Rotel",
            Password = BCryptSimple("Test123")
        };
        var user3 = new User
        {
            Email = "sarah.cohen@uni.ac.il",
            FirstName = "Sarah",
            LastName = "Cohen",
            Password = BCryptSimple("Demo123")
        };
        var user4 = new User
        {
            Email = "david.levi@uni.ac.il",
            FirstName = "David",
            LastName = "Levi",
            Password = BCryptSimple("Demo123")
        };
        var user5 = new User
        {
            Email = "maya.alon@uni.ac.il",
            FirstName = "Maya",
            LastName = "Alon",
            Password = BCryptSimple("Demo123")
        };
        db.Users.AddRange(user1, user2, user3, user4, user5);

        // === Notification Settings ===
        db.NotificationSettings.AddRange(
            new NotificationSettings { Email = user1.Email, NotifyBeforeTask = true, DailyMorningSummary = true },
            new NotificationSettings { Email = user2.Email, NotifyBeforeTask = true },
            new NotificationSettings { Email = user3.Email },
            new NotificationSettings { Email = user4.Email },
            new NotificationSettings { Email = user5.Email }
        );

        // === Instructors ===
        var inst1 = new Instructor { InstructorName = "Dr. Sarah Miller" };
        var inst2 = new Instructor { InstructorName = "Prof. David Chen" };
        var inst3 = new Instructor { InstructorName = "Dr. Rachel Green" };
        db.Instructors.AddRange(inst1, inst2, inst3);
        db.SaveChanges(); // Save to get auto-generated InstructorIds

        // === Courses ===
        var courses = new[]
        {
            new Course { CourseId = 1, CourseName = "Introduction to Computer Science", WeeklyHours = 4, Credits = 3, Semester = "2025B", InstructorId = inst1.InstructorId },
            new Course { CourseId = 2, CourseName = "Data Structures & Algorithms", WeeklyHours = 5, Credits = 4, Semester = "2025B", InstructorId = inst2.InstructorId },
            new Course { CourseId = 3, CourseName = "Linear Algebra", WeeklyHours = 3, Credits = 3, Semester = "2025B", InstructorId = inst3.InstructorId },
            new Course { CourseId = 4, CourseName = "Probability & Statistics", WeeklyHours = 3, Credits = 3, Semester = "2025B", InstructorId = inst1.InstructorId },
            new Course { CourseId = 5, CourseName = "Web Development", WeeklyHours = 4, Credits = 3, Semester = "2025B", InstructorId = inst2.InstructorId }
        };
        db.Courses.AddRange(courses);

        // === UserCourses ===
        foreach (var c in courses)
        {
            db.UserCourses.Add(new UserCourse { Email = user1.Email, CourseId = c.CourseId });
        }
        db.UserCourses.AddRange(
            new UserCourse { Email = user2.Email, CourseId = courses[0].CourseId },
            new UserCourse { Email = user2.Email, CourseId = courses[1].CourseId },
            new UserCourse { Email = user2.Email, CourseId = courses[2].CourseId }
        );

        // === Exams ===
        db.Exams.AddRange(
            new Exam { CourseId = 1, Date = new DateTime(2026, 2, 20), Time = new TimeSpan(9, 0, 0), Session = "A", Duration = 120 },
            new Exam { CourseId = 1, Date = new DateTime(2026, 3, 15), Time = new TimeSpan(9, 0, 0), Session = "A", Duration = 180 },
            new Exam { CourseId = 2, Date = new DateTime(2026, 2, 25), Time = new TimeSpan(14, 0, 0), Session = "B", Duration = 150 },
            new Exam { CourseId = 3, Date = new DateTime(2026, 3, 10), Time = new TimeSpan(10, 0, 0), Session = "A", Duration = 120 },
            new Exam { CourseId = 4, Date = new DateTime(2026, 2, 28), Time = new TimeSpan(11, 0, 0), Session = "A", Duration = 90 },
            new Exam { CourseId = 5, Date = new DateTime(2026, 3, 20), Time = new TimeSpan(14, 0, 0), Session = "B", Duration = 120 }
        );

        // === Tasks ===
        var tasks = new[]
        {
            // CS Intro tasks
            new StudentTask { CourseId = 1, Title = "Homework 1 - Variables & Loops", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 2, 5), IsCompleted = true, Priority = "Medium", Email = user1.Email },
            new StudentTask { CourseId = 1, Title = "Homework 2 - Functions & Arrays", Type = "Homework", EstimatedHours = 4, DueDate = new DateTime(2026, 2, 15), IsCompleted = false, Priority = "High", Email = user1.Email },
            new StudentTask { CourseId = 1, Title = "Project Proposal", Type = "Project", EstimatedHours = 6, DueDate = new DateTime(2026, 2, 18), IsCompleted = false, Priority = "High", Email = user1.Email },

            // Data Structures tasks
            new StudentTask { CourseId = 2, Title = "Lab 1 - Linked Lists", Type = "Lab", EstimatedHours = 2, DueDate = new DateTime(2026, 2, 3), IsCompleted = true, Priority = "Medium", Email = user1.Email },
            new StudentTask { CourseId = 2, Title = "Lab 2 - Binary Trees", Type = "Lab", EstimatedHours = 3, DueDate = new DateTime(2026, 2, 14), IsCompleted = false, Priority = "High", Email = user1.Email },
            new StudentTask { CourseId = 2, Title = "Assignment 1 - Sorting Algorithms", Type = "Assignment", EstimatedHours = 8, DueDate = new DateTime(2026, 2, 22), IsCompleted = false, Priority = "High", Email = user1.Email },

            // Linear Algebra tasks
            new StudentTask { CourseId = 3, Title = "Problem Set 1 - Matrices", Type = "Homework", EstimatedHours = 2, DueDate = new DateTime(2026, 2, 1), IsCompleted = true, Priority = "Low", Email = user1.Email },
            new StudentTask { CourseId = 3, Title = "Problem Set 2 - Eigenvalues", Type = "Homework", EstimatedHours = 4, DueDate = new DateTime(2026, 2, 16), IsCompleted = false, Priority = "Medium", Email = user1.Email },

            // Web Dev tasks
            new StudentTask { CourseId = 5, Title = "HTML/CSS Assignment", Type = "Assignment", EstimatedHours = 3, DueDate = new DateTime(2026, 2, 2), IsCompleted = true, Priority = "Low", Email = user1.Email },
            new StudentTask { CourseId = 5, Title = "JavaScript Project", Type = "Project", EstimatedHours = 10, DueDate = new DateTime(2026, 2, 20), IsCompleted = false, Priority = "High", Email = user1.Email },
            new StudentTask { CourseId = 5, Title = "Final Project - Full Stack App", Type = "Project", EstimatedHours = 20, DueDate = new DateTime(2026, 3, 15), IsCompleted = false, Priority = "High", Email = user1.Email },

            // Probability tasks
            new StudentTask { CourseId = 4, Title = "Exercise 1 - Basic Probability", Type = "Homework", EstimatedHours = 2, DueDate = new DateTime(2026, 2, 12), IsCompleted = false, Priority = "Medium", Email = user1.Email },
            new StudentTask { CourseId = 4, Title = "Exercise 2 - Distributions", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 2, 19), IsCompleted = false, Priority = "Medium", Email = user1.Email },

            // User2 tasks
            new StudentTask { CourseId = 1, Title = "Homework 1 - Variables", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 2, 10), IsCompleted = false, Priority = "High", Email = user2.Email },
            new StudentTask { CourseId = 2, Title = "Lab 1 - Stacks", Type = "Lab", EstimatedHours = 2, DueDate = new DateTime(2026, 2, 12), IsCompleted = false, Priority = "Medium", Email = user2.Email }
        };
        db.Tasks.AddRange(tasks);

        db.SaveChanges();

        // === Events (need IDs from SaveChanges) ===
        var now = DateTime.Now;
        var monday = now.Date.AddDays(-(int)now.DayOfWeek + 1);

        // Class events for demo user
        var classEvents = new[]
        {
            // CS Intro: Mon/Wed 9:00-10:30
            new ClassEvent { Email = user1.Email, From = monday.AddHours(9), To = monday.AddHours(10.5), Recurring = true, CourseId = 1, Location = "Room 101", Duration = 1.5m },
            new ClassEvent { Email = user1.Email, From = monday.AddDays(2).AddHours(9), To = monday.AddDays(2).AddHours(10.5), Recurring = true, CourseId = 1, Location = "Room 101", Duration = 1.5m },

            // Data Structures: Tue/Thu 11:00-12:30
            new ClassEvent { Email = user1.Email, From = monday.AddDays(1).AddHours(11), To = monday.AddDays(1).AddHours(12.5), Recurring = true, CourseId = 2, Location = "Room 205", Duration = 1.5m },
            new ClassEvent { Email = user1.Email, From = monday.AddDays(3).AddHours(11), To = monday.AddDays(3).AddHours(12.5), Recurring = true, CourseId = 2, Location = "Room 205", Duration = 1.5m },

            // Linear Algebra: Mon/Wed 14:00-15:30
            new ClassEvent { Email = user1.Email, From = monday.AddHours(14), To = monday.AddHours(15.5), Recurring = true, CourseId = 3, Location = "Room 303", Duration = 1.5m },
            new ClassEvent { Email = user1.Email, From = monday.AddDays(2).AddHours(14), To = monday.AddDays(2).AddHours(15.5), Recurring = true, CourseId = 3, Location = "Room 303", Duration = 1.5m },

            // Web Dev: Fri 10:00-13:00
            new ClassEvent { Email = user1.Email, From = monday.AddDays(4).AddHours(10), To = monday.AddDays(4).AddHours(13), Recurring = true, CourseId = 5, Location = "Lab 401", Duration = 3.0m },

            // Probability: Tue/Thu 14:00-15:30
            new ClassEvent { Email = user1.Email, From = monday.AddDays(1).AddHours(14), To = monday.AddDays(1).AddHours(15.5), Recurring = true, CourseId = 4, Location = "Room 102", Duration = 1.5m },
            new ClassEvent { Email = user1.Email, From = monday.AddDays(3).AddHours(14), To = monday.AddDays(3).AddHours(15.5), Recurring = true, CourseId = 4, Location = "Room 102", Duration = 1.5m },
        };
        db.ClassEvents.AddRange(classEvents);

        // Work events
        db.WorkEvents.AddRange(
            new WorkEvent { Email = user1.Email, From = monday.AddDays(5).AddHours(9), To = monday.AddDays(5).AddHours(17), Recurring = true, TravelTime = 30, WorkPlace = "Tech Startup Inc." },
            new WorkEvent { Email = user1.Email, From = monday.AddDays(5).AddDays(7).AddHours(9), To = monday.AddDays(5).AddDays(7).AddHours(17), Recurring = true, TravelTime = 30, WorkPlace = "Tech Startup Inc." }
        );

        // Task events (calendar blocks for working on specific tasks)
        db.TaskEvents.AddRange(
            new TaskEvent { Email = user1.Email, From = monday.AddDays(1).AddHours(16), To = monday.AddDays(1).AddHours(18), Recurring = false, TaskId = tasks[1].TaskId, Priority = "High", Status = "Scheduled" },
            new TaskEvent { Email = user1.Email, From = monday.AddDays(3).AddHours(16), To = monday.AddDays(3).AddHours(19), Recurring = false, TaskId = tasks[5].TaskId, Priority = "High", Status = "In Progress" },
            new TaskEvent { Email = user1.Email, From = monday.AddDays(4).AddHours(14), To = monday.AddDays(4).AddHours(17), Recurring = false, TaskId = tasks[9].TaskId, Priority = "High", Status = "Scheduled" }
        );

        // Personal events
        db.PersonalEvents.AddRange(
            new PersonalEvent { Email = user1.Email, From = monday.AddHours(7), To = monday.AddHours(8), Recurring = true, Type = "Exercise", Description = "Morning gym session" },
            new PersonalEvent { Email = user1.Email, From = monday.AddDays(2).AddHours(7), To = monday.AddDays(2).AddHours(8), Recurring = true, Type = "Exercise", Description = "Morning gym session" },
            new PersonalEvent { Email = user1.Email, From = monday.AddDays(4).AddHours(7), To = monday.AddDays(4).AddHours(8), Recurring = true, Type = "Exercise", Description = "Morning gym session" },
            new PersonalEvent { Email = user1.Email, From = monday.AddDays(3).AddHours(18), To = monday.AddDays(3).AddHours(20), Recurring = true, Type = "Social", Description = "Study group meeting" },
            new PersonalEvent { Email = user1.Email, From = monday.AddDays(6).AddHours(10), To = monday.AddDays(6).AddHours(12), Recurring = false, Type = "Errand", Description = "Doctor appointment" }
        );

        // === Friend Requests ===
        db.FriendRequests.AddRange(
            new FriendRequest { RequesterEmail = user1.Email, AddresseeEmail = user3.Email, Status = "Accepted", RequestedAt = new DateTime(2026, 1, 15), RespondedAt = new DateTime(2026, 1, 15) },
            new FriendRequest { RequesterEmail = user1.Email, AddresseeEmail = user4.Email, Status = "Accepted", RequestedAt = new DateTime(2026, 1, 22), RespondedAt = new DateTime(2026, 1, 22) },
            new FriendRequest { RequesterEmail = user2.Email, AddresseeEmail = user3.Email, Status = "Accepted", RequestedAt = new DateTime(2026, 1, 20), RespondedAt = new DateTime(2026, 1, 20) },
            new FriendRequest { RequesterEmail = user5.Email, AddresseeEmail = user1.Email, Status = "Pending", RequestedAt = new DateTime(2026, 2, 5) },
            new FriendRequest { RequesterEmail = user5.Email, AddresseeEmail = user2.Email, Status = "Pending", RequestedAt = new DateTime(2026, 2, 6) }
        );

        // === Friendships (normalized pairs: Email1 < Email2 alphabetically) ===
        db.Friendships.AddRange(
            new Friendship { Email1 = user1.Email, Email2 = user3.Email, CreatedAt = new DateTime(2026, 1, 15), IsActive = true },   // demo < sarah
            new Friendship { Email1 = user4.Email, Email2 = user1.Email, CreatedAt = new DateTime(2026, 1, 22), IsActive = true },   // david < demo
            new Friendship { Email1 = user3.Email, Email2 = user2.Email, CreatedAt = new DateTime(2026, 1, 20), IsActive = true }    // sarah < yuval
        );

        db.SaveChanges();
    }

    private static string BCryptSimple(string password)
    {
        // Simple hash for demo purposes - in production use proper BCrypt
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "SmartStudySalt2026"));
        return Convert.ToBase64String(bytes);
    }
}
