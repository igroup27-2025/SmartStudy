using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly ILogger<NotificationBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    public NotificationBackgroundService(ILogger<NotificationBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                GenerateNotificationsForAllUsers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NotificationBackgroundService");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private void GenerateNotificationsForAllUsers()
    {
        var db = new DBservices();
        var userEmails = db.GetAllUserEmails();

        foreach (var email in userEmails)
        {
            try
            {
                Notification.GenerateDeadline(email);

                try
                {
                    var stressResult = User.GetStressScore(email);
                    if (stressResult != null)
                    {
                        Notification.GenerateOverload(email, stressResult.Score);
                    }
                }
                catch { /* skip stress for this user if it fails */ }

                var hour = DateTime.Now.Hour;
                if (hour >= 6 && hour <= 10)
                {
                    Notification.GenerateDailySummary(email);
                }

                var dayOfWeek = DateTime.Now.DayOfWeek;
                if ((dayOfWeek == DayOfWeek.Sunday || dayOfWeek == DayOfWeek.Monday) && hour >= 6 && hour <= 10)
                {
                    Notification.GenerateWeeklyPlanReminder(email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate notifications for {Email}", email);
            }
        }
    }
}
