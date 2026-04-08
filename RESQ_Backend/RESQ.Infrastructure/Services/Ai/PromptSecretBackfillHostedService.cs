using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services.Ai;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Ai;

public class PromptSecretBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    IPromptSecretProtector promptSecretProtector,
    ILogger<PromptSecretBackfillHostedService> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IPromptSecretProtector _promptSecretProtector = promptSecretProtector;
    private readonly ILogger<PromptSecretBackfillHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_promptSecretProtector.HasActiveKey)
        {
            _logger.LogWarning("Prompt secret backfill skipped because PromptSecrets:MasterKey is not configured.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
            var prompts = await dbContext.Prompts
                .Where(prompt => prompt.ApiKey != null && prompt.ApiKey != string.Empty)
                .ToListAsync(cancellationToken);

            var updated = 0;
            foreach (var prompt in prompts)
            {
                if (_promptSecretProtector.IsProtected(prompt.ApiKey))
                {
                    continue;
                }

                prompt.ApiKey = _promptSecretProtector.Protect(prompt.ApiKey);
                prompt.UpdatedAt = DateTime.UtcNow;
                updated++;
            }

            if (updated > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Encrypted {count} legacy prompt API key(s) in the database.", updated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prompt secret backfill failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}