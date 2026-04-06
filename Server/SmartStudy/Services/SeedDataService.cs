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
            Password = BCryptSimple("Demo123"),
            RuppinetId = "315715797"
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

        db.SaveChanges();

        // === Events ===
        // Class events, exams, and courses come from Ruppinet sync — no seed data needed for those.
        // Tasks are also created after sync via SeedTasksForSyncedCourses.
        var now = DateTime.Now;
        var monday = now.Date.AddDays(-(int)now.DayOfWeek + 1);

        // Work events
        db.WorkEvents.AddRange(
            new WorkEvent { Email = user1.Email, From = monday.AddDays(5).AddHours(9), To = monday.AddDays(5).AddHours(17), Recurring = true, TravelTime = 30, WorkPlace = "Tech Startup Inc." },
            new WorkEvent { Email = user1.Email, From = monday.AddDays(5).AddDays(7).AddHours(9), To = monday.AddDays(5).AddDays(7).AddHours(17), Recurring = true, TravelTime = 30, WorkPlace = "Tech Startup Inc." }
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
            new Friendship { Email1 = user1.Email, Email2 = user3.Email, CreatedAt = new DateTime(2026, 1, 15), IsActive = true },
            new Friendship { Email1 = user4.Email, Email2 = user1.Email, CreatedAt = new DateTime(2026, 1, 22), IsActive = true },
            new Friendship { Email1 = user3.Email, Email2 = user2.Email, CreatedAt = new DateTime(2026, 1, 20), IsActive = true }
        );

        db.SaveChanges();
    }

    /// <summary>
    /// Create demo tasks for Ruppinet-synced courses. Called after first sync for a user.
    /// Only creates tasks if the user has no tasks yet.
    /// </summary>
    public static void SeedTasksForSyncedCourses(SmartStudyDbContext db, string email)
    {
        if (db.Tasks.Any(t => t.Email == email)) return;

        var enrolledCourseIds = db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToHashSet();

        var tasks = new List<StudentTask>();

        // פיתוח מערכות מידע אינטרנטיות 2
        if (enrolledCourseIds.Contains(129485))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129485, Title = "תרגיל 1 - REST API", Type = "Homework", EstimatedHours = 4, DueDate = new DateTime(2026, 4, 15), IsCompleted = true, Priority = "Medium", Email = email },
                new StudentTask { CourseId = 129485, Title = "תרגיל 2 - React Components", Type = "Homework", EstimatedHours = 5, DueDate = new DateTime(2026, 4, 29), IsCompleted = false, Priority = "High", Email = email },
                new StudentTask { CourseId = 129485, Title = "פרויקט אמצע - Full Stack App", Type = "Project", EstimatedHours = 15, DueDate = new DateTime(2026, 5, 20), IsCompleted = false, Priority = "High", Email = email },
            });
        }

        // תקשורת נתונים ואבטחת מידע
        if (enrolledCourseIds.Contains(128777))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 128777, Title = "תרגיל 1 - פרוטוקולי תקשורת", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 4, 13), IsCompleted = true, Priority = "Medium", Email = email },
                new StudentTask { CourseId = 128777, Title = "תרגיל 2 - הצפנה וחתימות דיגיטליות", Type = "Homework", EstimatedHours = 4, DueDate = new DateTime(2026, 5, 4), IsCompleted = false, Priority = "High", Email = email },
                new StudentTask { CourseId = 128777, Title = "מעבדה - Wireshark Analysis", Type = "Lab", EstimatedHours = 3, DueDate = new DateTime(2026, 5, 11), IsCompleted = false, Priority = "Medium", Email = email },
            });
        }

        // סוגיות יישומיות בלמידת מכונה
        if (enrolledCourseIds.Contains(129729))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129729, Title = "תרגיל 1 - Linear Regression", Type = "Homework", EstimatedHours = 4, DueDate = new DateTime(2026, 4, 20), IsCompleted = true, Priority = "Medium", Email = email },
                new StudentTask { CourseId = 129729, Title = "תרגיל 2 - Neural Networks", Type = "Homework", EstimatedHours = 6, DueDate = new DateTime(2026, 5, 8), IsCompleted = false, Priority = "High", Email = email },
                new StudentTask { CourseId = 129729, Title = "פרויקט סופי - מודל ML", Type = "Project", EstimatedHours = 20, DueDate = new DateTime(2026, 6, 15), IsCompleted = false, Priority = "High", Email = email },
            });
        }

        // אפיון ועיצוב ממשק משתמשים
        if (enrolledCourseIds.Contains(129076))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129076, Title = "תרגיל 1 - User Research", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 4, 14), IsCompleted = true, Priority = "Low", Email = email },
                new StudentTask { CourseId = 129076, Title = "תרגיל 2 - Wireframes & Prototyping", Type = "Homework", EstimatedHours = 5, DueDate = new DateTime(2026, 5, 1), IsCompleted = false, Priority = "Medium", Email = email },
                new StudentTask { CourseId = 129076, Title = "פרויקט - UI Design System", Type = "Project", EstimatedHours = 12, DueDate = new DateTime(2026, 6, 1), IsCompleted = false, Priority = "High", Email = email },
            });
        }

        // פרוייקט גמר 1
        if (enrolledCourseIds.Contains(129149))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129149, Title = "הגשת הצעת פרויקט", Type = "Project", EstimatedHours = 8, DueDate = new DateTime(2026, 4, 10), IsCompleted = true, Priority = "High", Email = email },
                new StudentTask { CourseId = 129149, Title = "Sprint 1 - Backend Core", Type = "Project", EstimatedHours = 20, DueDate = new DateTime(2026, 5, 15), IsCompleted = false, Priority = "High", Email = email },
                new StudentTask { CourseId = 129149, Title = "Sprint 2 - Frontend Integration", Type = "Project", EstimatedHours = 20, DueDate = new DateTime(2026, 6, 10), IsCompleted = false, Priority = "High", Email = email },
            });
        }

        // פרוייקט גמר 2
        if (enrolledCourseIds.Contains(129138))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129138, Title = "Sprint 3 - Testing & Polish", Type = "Project", EstimatedHours = 15, DueDate = new DateTime(2026, 5, 20), IsCompleted = false, Priority = "High", Email = email },
                new StudentTask { CourseId = 129138, Title = "הכנת מצגת סופית", Type = "Project", EstimatedHours = 6, DueDate = new DateTime(2026, 6, 20), IsCompleted = false, Priority = "Medium", Email = email },
            });
        }

        // תעשייה 4.0
        if (enrolledCourseIds.Contains(129712))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 129712, Title = "סיכום מאמר - Industry 4.0", Type = "Homework", EstimatedHours = 3, DueDate = new DateTime(2026, 4, 22), IsCompleted = false, Priority = "Medium", Email = email },
                new StudentTask { CourseId = 129712, Title = "עבודת סיכום - IoT & Smart Factory", Type = "Assignment", EstimatedHours = 8, DueDate = new DateTime(2026, 5, 25), IsCompleted = false, Priority = "Medium", Email = email },
            });
        }

        // מיומנויות לעולם העבודה
        if (enrolledCourseIds.Contains(130524))
        {
            tasks.AddRange(new[]
            {
                new StudentTask { CourseId = 130524, Title = "LinkedIn Profile Setup", Type = "Assignment", EstimatedHours = 2, DueDate = new DateTime(2026, 4, 18), IsCompleted = true, Priority = "Low", Email = email },
                new StudentTask { CourseId = 130524, Title = "Presentation - Career Plan", Type = "Assignment", EstimatedHours = 4, DueDate = new DateTime(2026, 5, 10), IsCompleted = false, Priority = "Medium", Email = email },
            });
        }

        if (tasks.Count > 0)
        {
            db.Tasks.AddRange(tasks);
            db.SaveChanges();
        }
    }

    private static string BCryptSimple(string password)
    {
        // Simple hash for demo purposes - in production use proper BCrypt
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "SmartStudySalt2026"));
        return Convert.ToBase64String(bytes);
    }

    private static string EncryptRuppinetPassword(string plainText)
    {
        var configKey = "SmartStudyRuppinetEncKey2026!aa";
        using var shaKey = System.Security.Cryptography.SHA256.Create();
        var key = shaKey.ComputeHash(System.Text.Encoding.UTF8.GetBytes(configKey));

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }
}
