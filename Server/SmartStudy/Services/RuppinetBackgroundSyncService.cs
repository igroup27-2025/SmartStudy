using SmartStudy.DAL;

namespace SmartStudy.Services;

// Hosted service that periodically runs Ruppinet sync for all users with stale data.
public class RuppinetBackgroundSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RuppinetBackgroundSyncService> _logger;
    private readonly TimeSpan _interval;

    // Injects DI scope factory, logger, and reads sync interval from configuration.
    public RuppinetBackgroundSyncService(IServiceScopeFactory scopeFactory,
        ILogger<RuppinetBackgroundSyncService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        var hours = int.TryParse(config["Ruppinet:BackgroundSyncIntervalHours"], out var h) ? h : 6;
        _interval = TimeSpan.FromHours(hours);
    }

    // Background loop — waits 2 minutes, then runs SyncAllUsers on each interval tick.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
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
                _logger.LogError(ex, "Error in RuppinetBackgroundSyncService");
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

    // Picks users whose last sync is older than the cutoff and syncs each in its own scope.
    private async Task SyncAllUsersAsync()
    {
        var dal = new DBservices();
        var syncIntervalHours = int.TryParse(_config["Ruppinet:SyncIntervalHours"], out var h) ? h : 12;
        var cutoff = DateTime.UtcNow.AddHours(-syncIntervalHours);

        var usersToSync = dal.GetUsersForRuppinetSync(cutoff);

        if (usersToSync.Count == 0) return;

        _logger.LogInformation("Ruppinet background sync starting for {Count} users", usersToSync.Count);

        foreach (var email in usersToSync)
        {
            try
            {
                using var userScope = _scopeFactory.CreateScope();
                var syncService = userScope.ServiceProvider.GetRequiredService<RuppinetSyncService>();
                var result = await syncService.SyncAllAsync(email);
                if (result.Success)
                    _logger.LogInformation("Background sync completed for {Email}: {Message}", email, result.Message);
                else
                    _logger.LogWarning("Background sync failed for {Email}: {Message}", email, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background sync error for {Email}", email);
            }
        }
    }
}
