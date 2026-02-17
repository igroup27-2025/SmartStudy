using Microsoft.EntityFrameworkCore;
using SmartStudy.Models;

namespace SmartStudy.Data;

public class SmartStudyDbContext : DbContext
{
    public SmartStudyDbContext(DbContextOptions<SmartStudyDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<SchedulingPreferences> SchedulingPreferences => Set<SchedulingPreferences>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<UserCourse> UserCourses => Set<UserCourse>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<StudentTask> Tasks => Set<StudentTask>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<ClassEvent> ClassEvents => Set<ClassEvent>();
    public DbSet<TaskEvent> TaskEvents => Set<TaskEvent>();
    public DbSet<WorkEvent> WorkEvents => Set<WorkEvent>();
    public DbSet<PersonalEvent> PersonalEvents => Set<PersonalEvent>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<SharedTask> SharedTasks => Set<SharedTask>();
    public DbSet<SharedTaskMember> SharedTaskMembers => Set<SharedTaskMember>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Users =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("SmartStudy_Users");
            entity.HasKey(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Password).HasColumnName("Password").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ResetToken).HasMaxLength(50);
            entity.Property(e => e.ResetTokenExpiry);
            entity.Property(e => e.AuthProvider).HasMaxLength(20);

            entity.Property(e => e.OnboardingCompleted).HasDefaultValue(false);
        });

        // ===== NotificationSettings =====
        modelBuilder.Entity<NotificationSettings>(entity =>
        {
            entity.ToTable("SmartStudy_NotificationSettings");
            entity.HasKey(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.NotifyBeforeTask).HasColumnName("Notify_before_task").HasDefaultValue(false);
            entity.Property(e => e.DailyMorningSummary).HasColumnName("Daily_morning_summary").HasDefaultValue(false);
            entity.Property(e => e.WeeklyPlanReminder).HasColumnName("Weekly_plan_reminder").HasDefaultValue(false);
            entity.Property(e => e.EnablePushNotification).HasColumnName("Enable_push_notification").HasDefaultValue(false);
            entity.Property(e => e.QuietHoursStart).HasColumnName("Quiet_hours_start");
            entity.Property(e => e.QuietHoursEnd).HasColumnName("Quiet_hours_end");

            entity.HasOne(e => e.User)
                .WithOne(u => u.NotificationSettings)
                .HasForeignKey<NotificationSettings>(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== SchedulingPreferences =====
        modelBuilder.Entity<SchedulingPreferences>(entity =>
        {
            entity.ToTable("SmartStudy_SchedulingPreferences");
            entity.HasKey(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.MaxDailyStudyHours).HasDefaultValue(6.0);
            entity.Property(e => e.MaxContinuousMinutes).HasDefaultValue(90);
            entity.Property(e => e.DayStartHour).HasDefaultValue(8);
            entity.Property(e => e.DayEndHour).HasDefaultValue(22);
            entity.Property(e => e.SleepHoursPerDay).HasDefaultValue(8.0);
            entity.Property(e => e.LunchBreakStart);
            entity.Property(e => e.LunchBreakEnd);
            entity.Property(e => e.BreakDurationMinutes).HasDefaultValue(15);
            entity.Property(e => e.DefaultTaskEstimatedHours).HasDefaultValue(4.0);
            entity.Property(e => e.MaxDailyTotalHours).HasDefaultValue(14.0);
            entity.Property(e => e.ExamPrepHoursPerDay).HasDefaultValue(5.0);
            entity.Property(e => e.ExamPrepDays).HasDefaultValue(3);

            entity.HasOne(e => e.User)
                .WithOne(u => u.SchedulingPreferences)
                .HasForeignKey<SchedulingPreferences>(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Instructors =====
        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.ToTable("SmartStudy_Instructors");
            entity.HasKey(e => e.InstructorId);
            entity.Property(e => e.InstructorName).HasMaxLength(200).IsRequired();
        });

        // ===== Courses =====
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("SmartStudy_Courses");
            entity.HasKey(e => e.CourseId);
            entity.Property(e => e.CourseId).ValueGeneratedNever();
            entity.Property(e => e.CourseName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WeeklyHours).HasColumnType("decimal(4,1)");
            entity.Property(e => e.Credits).HasColumnType("decimal(4,1)");
            entity.Property(e => e.Semester).HasMaxLength(50);
            entity.Property(e => e.DefaultTaskEstimatedHours);
            entity.Property(e => e.ExamPrepHoursPerDay);
            entity.Property(e => e.ExamPrepDays);

            entity.HasOne(e => e.Instructor)
                .WithMany(i => i.Courses)
                .HasForeignKey(e => e.InstructorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ===== UserCourses =====
        modelBuilder.Entity<UserCourse>(entity =>
        {
            entity.ToTable("SmartStudy_UserCourses");
            entity.HasKey(e => new { e.Email, e.CourseId });
            entity.Property(e => e.Email).HasMaxLength(255);

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserCourses)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.StudyPartnerEmail).HasMaxLength(255);

            entity.Property(e => e.SharedByDefault).HasDefaultValue(false);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.UserCourses)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Exams =====
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.ToTable("SmartStudy_Exams");
            entity.HasKey(e => e.ExamId);
            entity.Property(e => e.Date).HasColumnName("Date");
            entity.Property(e => e.Time).HasColumnName("Time");
            entity.Property(e => e.Session).HasColumnName("Session").HasMaxLength(10).IsRequired();

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Exams)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Tasks (StudentTask) =====
        modelBuilder.Entity<StudentTask>(entity =>
        {
            entity.ToTable("SmartStudy_Tasks");
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EstimatedHours).HasColumnType("decimal(5,2)");
            entity.Property(e => e.DueDate);
            entity.Property(e => e.IsCompleted).HasDefaultValue(false);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.ActualHours).HasColumnType("decimal(5,2)");
            entity.Property(e => e.ParentTaskId);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.AllowSplitting).HasDefaultValue(false);

            entity.HasOne(e => e.ParentTask)
                .WithMany(e => e.SubTasks)
                .HasForeignKey(e => e.ParentTaskId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Tasks)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ===== Events (TPT base) =====
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("SmartStudy_Events");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.From).HasColumnName("From");
            entity.Property(e => e.To).HasColumnName("To");
            entity.Property(e => e.Recurring).HasDefaultValue(false);
            entity.Property(e => e.RecurrenceEndDate).HasColumnName("RecurrenceEndDate");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Events)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== ClassEvents (TPT) =====
        modelBuilder.Entity<ClassEvent>(entity =>
        {
            entity.ToTable("SmartStudy_ClassEvents");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Duration).HasColumnType("decimal(5,2)");

            entity.HasOne(e => e.Course)
                .WithMany(c => c.ClassEvents)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== TaskEvents (TPT) =====
        modelBuilder.Entity<TaskEvent>(entity =>
        {
            entity.ToTable("SmartStudy_TaskEvents");
            entity.Property(e => e.Priority).HasMaxLength(50);
            entity.Property(e => e.ActualHours).HasColumnType("decimal(5,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50);

            entity.HasOne(e => e.StudentTask)
                .WithMany(t => t.TaskEvents)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ===== WorkEvents (TPT) =====
        modelBuilder.Entity<WorkEvent>(entity =>
        {
            entity.ToTable("SmartStudy_WorkEvents");
            entity.Property(e => e.WorkPlace).HasMaxLength(200);
        });

        // ===== PersonalEvents (TPT) =====
        modelBuilder.Entity<PersonalEvent>(entity =>
        {
            entity.ToTable("SmartStudy_PersonalEvents");
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(50);
            entity.Property(e => e.Description).HasColumnName("Description");
        });

        // ===== FriendRequests =====
        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.ToTable("SmartStudy_FriendRequests");
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.RequesterEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.AddresseeEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt);
            entity.Property(e => e.RespondedAt);

            entity.HasOne(e => e.Requester)
                .WithMany(u => u.SentFriendRequests)
                .HasForeignKey(e => e.RequesterEmail)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Addressee)
                .WithMany(u => u.ReceivedFriendRequests)
                .HasForeignKey(e => e.AddresseeEmail)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasCheckConstraint("CK_FriendRequest_NotSelf", "[RequesterEmail] <> [AddresseeEmail]");

            entity.HasIndex(e => new { e.RequesterEmail, e.AddresseeEmail })
                .HasFilter("[Status] = 'Pending'")
                .IsUnique();
        });

        // ===== Friendships =====
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("SmartStudy_Friendships");
            entity.HasKey(e => e.FriendshipId);
            entity.Property(e => e.Email1).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email2).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.User1)
                .WithMany(u => u.FriendshipsAsUser1)
                .HasForeignKey(e => e.Email1)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User2)
                .WithMany(u => u.FriendshipsAsUser2)
                .HasForeignKey(e => e.Email2)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasCheckConstraint("CK_Friendship_NotSelf", "[Email1] <> [Email2]");

            entity.HasIndex(e => new { e.Email1, e.Email2 }).IsUnique();
        });

        // ===== SharedTasks =====
        modelBuilder.Entity<SharedTask>(entity =>
        {
            entity.ToTable("SmartStudy_SharedTasks");
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.CreatedByEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.SharedStatus).HasMaxLength(20).HasDefaultValue("Draft");
            entity.Property(e => e.CreatedAt);

            entity.HasOne(e => e.Task)
                .WithOne(t => t.SharedTask)
                .HasForeignKey<SharedTask>(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByEmail)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ===== SharedTaskMembers =====
        modelBuilder.Entity<SharedTaskMember>(entity =>
        {
            entity.ToTable("SmartStudy_SharedTaskMembers");
            entity.HasKey(e => new { e.TaskId, e.Email });
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.ResponseStatus).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.RespondedAt);

            entity.HasOne(e => e.SharedTask)
                .WithMany(st => st.Members)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.SharedTaskMemberships)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ===== Notifications =====
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("SmartStudy_Notifications");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.RelatedEntityId);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.Email, e.CreatedAt });
        });
    }
}
