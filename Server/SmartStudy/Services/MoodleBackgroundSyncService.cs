using SmartStudy.DAL;

namespace SmartStudy.Services;

// Hosted service that periodically runs Moodle sync for all users with stale data.
public class MoodleBackgroundSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MoodleBackgroundSyncService> _logger;
    private readonly TimeSpan _interval;

    // Injects DI scope factory, logger, and reads sync interval from configuration.
    public MoodleBackgroundSyncService(IServiceScopeFactory scopeFactory,
        ILogger<MoodleBackgroundSyncService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        var hours = int.TryParse(config["Moodle:BackgroundSyncIntervalHours"], out var h) ? h : 12;
        _interval = TimeSpan.FromHours(hours);
    }

    // Background loop — waits 3 minutes, then runs SyncAllUsers on each interval tick.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
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

    // Picks users whose last Moodle sync is older than the cutoff and syncs each in its own scope.
    private async Task SyncAllUsersAsync()
    {
        var dal = new DBservices();
        var syncIntervalHours = int.TryParse(_config["Moodle:SyncIntervalHours"], out var h) ? h : 24;
        var cutoff = DateTime.UtcNow.AddHours(-syncIntervalHours);

        var usersToSync = dal.GetUsersForMoodleSync(cutoff);

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
