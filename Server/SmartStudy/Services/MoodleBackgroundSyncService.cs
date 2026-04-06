using SmartStudy.DAL;

namespace SmartStudy.Services;

public class MoodleBackgroundSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MoodleBackgroundSyncService> _logger;
    private readonly TimeSpan _interval;

    public MoodleBackgroundSyncService(IServiceScopeFactory scopeFactory,
        ILogger<MoodleBackgroundSyncService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = int.TryParse(config["Moodle:BackgroundSyncIntervalHours"], out var h) ? h : 12;
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait before first run to let the app fully start
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MoodleBackgroundSyncService");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task SyncAllUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dal = scope.ServiceProvider.GetRequiredService<DBservices>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var syncIntervalHours = int.TryParse(config["Moodle:SyncIntervalHours"], out var h) ? h : 24;
        var cutoff = DateTime.UtcNow.AddHours(-syncIntervalHours);

        var usersToSync = await dal.GetUsersForMoodleSyncAsync(cutoff);

        if (usersToSync.Count == 0) return;

        _logger.LogInformation("Moodle background sync starting for {Count} users", usersToSync.Count);

        foreach (var email in usersToSync)
        {
            try
            {
                using var userScope = _scopeFactory.CreateScope();
                var syncService = userScope.ServiceProvider.GetRequiredService<MoodleSyncService>();
                var result = await syncService.SyncAllAsync(email);
                if (result.Success)
                    _logger.LogInformation("Moodle background sync completed for {Email}: {Message}", email, result.Message);
                else
                    _logger.LogWarning("Moodle background sync failed for {Email}: {Message}", email, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Moodle background sync error for {Email}", email);
            }
        }
    }
}
