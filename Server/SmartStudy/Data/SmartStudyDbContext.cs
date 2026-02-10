using Microsoft.EntityFrameworkCore;
using SmartStudy.Models;

namespace SmartStudy.Data;

public class SmartStudyDbContext : DbContext
{
    public SmartStudyDbContext(DbContextOptions<SmartStudyDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
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
    public DbSet<StudyConnection> StudyConnections => Set<StudyConnection>();

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

            entity.HasOne(e => e.User)
                .WithOne(u => u.NotificationSettings)
                .HasForeignKey<NotificationSettings>(e => e.Email)
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
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Tasks)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(e => e.Email)
                .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.Cascade);
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

        // ===== StudyConnections =====
        modelBuilder.Entity<StudyConnection>(entity =>
        {
            entity.ToTable("SmartStudy_StudyConnections");
            entity.HasKey(e => e.ConnectionId);
            entity.Property(e => e.RequesterEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ReceiverEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.AcceptedAt);

            entity.HasOne(e => e.Requester)
                .WithMany(u => u.SentConnections)
                .HasForeignKey(e => e.RequesterEmail)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Receiver)
                .WithMany(u => u.ReceivedConnections)
                .HasForeignKey(e => e.ReceiverEmail)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
