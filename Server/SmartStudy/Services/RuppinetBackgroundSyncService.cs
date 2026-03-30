using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;

namespace SmartStudy.Services;

public class RuppinetBackgroundSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuppinetBackgroundSyncService> _logger;
    private readonly TimeSpan _interval;

    public RuppinetBackgroundSyncService(IServiceScopeFactory scopeFactory,
        ILogger<RuppinetBackgroundSyncService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = int.TryParse(config["Ruppinet:BackgroundSyncIntervalHours"], out var h) ? h : 6;
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait before first run to let the app fully start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

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

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SyncAllUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartStudyDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var syncIntervalHours = int.TryParse(config["Ruppinet:SyncIntervalHours"], out var h) ? h : 12;
        var cutoff = DateTime.UtcNow.AddHours(-syncIntervalHours);

        var usersToSync = await db.Users
            .Where(u => u.RuppinetId != null && u.RuppinetPassword != null
                && (u.LastRuppinetSync == null || u.LastRuppinetSync < cutoff))
            .Select(u => u.Email)
            .ToListAsync();

        if (usersToSync.Count == 0) return;

        _logger.LogInformation("Ruppinet background sync starting for {Count} users", usersToSync.Count);

        foreach (var email in usersToSync)
        {
            try
            {
                // Create a fresh scope per user so one failure doesn't affect others
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
