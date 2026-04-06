using SmartStudy.DAL;

namespace SmartStudy.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    public NotificationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait a bit before first run to let the app fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return; // App is shutting down, exit gracefully
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateNotificationsForAllUsersAsync();
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
                return; // App is shutting down, exit gracefully
            }
        }
    }

    private async Task GenerateNotificationsForAllUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DBservices>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var stressService = scope.ServiceProvider.GetRequiredService<StressService>();

        var userEmails = await db.GetAllUserEmailsAsync();

        foreach (var email in userEmails)
        {
            try
            {
                // Deadline notifications
                await notificationService.GenerateDeadlineNotificationsAsync(email);

                // Overload notifications (need stress score)
                try
                {
                    var stressResult = await stressService.GetStressScoreAsync(email);
                    if (stressResult != null)
                    {
                        await notificationService.GenerateOverloadNotificationAsync(email, stressResult.Score);
                    }
                }
                catch { /* skip stress for this user if it fails */ }

                // Daily summary (only before 10 AM makes sense, but generate whenever - dedup handles repeats)
                var hour = DateTime.Now.Hour;
                if (hour >= 6 && hour <= 10)
                {
                    await notificationService.GenerateDailySummaryAsync(email);
                }

                // Weekly reminder (Sunday or Monday morning)
                var dayOfWeek = DateTime.Now.DayOfWeek;
                if ((dayOfWeek == DayOfWeek.Sunday || dayOfWeek == DayOfWeek.Monday) && hour >= 6 && hour <= 10)
                {
                    await notificationService.GenerateWeeklyPlanReminderAsync(email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate notifications for {Email}", email);
            }
        }
    }
}
