using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Identity;

public class UnverifiedUserCleanupBackgroundService : BackgroundService
{
    private readonly ILogger<UnverifiedUserCleanupBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UnverifiedUserCleanupBackgroundService(
        ILogger<UnverifiedUserCleanupBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Unverified User Cleanup Background Service is starting.");

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupUnverifiedUsersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up unverified users.");
            }

            // Run periodically (e.g., every 1 hour)
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupUnverifiedUsersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResQDbContext>();

        var expirationThreshold = DateTime.UtcNow.AddHours(-24);

        // Delete directly in SQL to avoid loading the full User entity graph and spatial columns.
        var deletedCount = await context.Users
            .Where(u => !u.IsEmailVerified 
                        && u.EmailVerificationToken != null 
                        && u.CreatedAt <= expirationThreshold)
            .ExecuteDeleteAsync(stoppingToken);

        if (deletedCount == 0)
            return;

        _logger.LogInformation("Auto-deleted {Count} unverified users who expired after 24 hours.", deletedCount);
    }
}
