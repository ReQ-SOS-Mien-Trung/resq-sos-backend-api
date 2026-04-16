using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Ai;

public class AiConfigBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    IAiSecretProtector aiSecretProtector,
    ILogger<AiConfigBackfillHostedService> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IAiSecretProtector _aiSecretProtector = aiSecretProtector;
    private readonly ILogger<AiConfigBackfillHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
            var connection = dbContext.Database.GetDbConnection();

            await connection.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(connection, "ai_configs", cancellationToken))
            {
                _logger.LogWarning("AI config backfill skipped because table 'ai_configs' does not exist yet.");
                return;
            }

            if (await dbContext.AiConfigs.AnyAsync(cancellationToken))
            {
                await EncryptLegacyAiConfigSecretsAsync(dbContext, cancellationToken);
                return;
            }

            if (!await LegacyPromptConfigColumnsExistAsync(connection, cancellationToken))
            {
                _logger.LogInformation("AI config backfill skipped because legacy prompt config columns are absent.");
                return;
            }

            var legacyConfigs = await ReadLegacyPromptConfigsAsync(connection, cancellationToken);
            if (legacyConfigs.Count == 0)
            {
                _logger.LogInformation("AI config backfill found no legacy prompt-level AI config rows.");
                return;
            }

            var activeSource = legacyConfigs
                .Where(config => config.IsActive && config.PromptType == PromptType.MissionPlanning)
                .OrderByDescending(config => config.UpdatedAt ?? config.CreatedAt)
                .ThenByDescending(config => config.PromptId)
                .FirstOrDefault();

            var groupedConfigs = legacyConfigs
                .GroupBy(config => new LegacyAiConfigKey(
                    config.Provider,
                    config.Model,
                    config.Temperature,
                    config.MaxTokens,
                    config.ApiUrl,
                    config.ApiKey))
                .Select(group => new
                {
                    Key = group.Key,
                    Representative = group
                        .OrderByDescending(config => config.IsActive && config.PromptType == PromptType.MissionPlanning)
                        .ThenByDescending(config => config.IsActive)
                        .ThenByDescending(config => config.UpdatedAt ?? config.CreatedAt)
                        .ThenByDescending(config => config.PromptId)
                        .First()
                })
                .OrderByDescending(group => group.Representative.IsActive && group.Representative.PromptType == PromptType.MissionPlanning)
                .ThenByDescending(group => group.Representative.IsActive)
                .ThenByDescending(group => group.Representative.UpdatedAt ?? group.Representative.CreatedAt)
                .ThenByDescending(group => group.Representative.PromptId)
                .ToList();

            var usedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeAssigned = false;
            var created = 0;

            foreach (var group in groupedConfigs)
            {
                var representative = group.Representative;
                var normalizedVersion = CreateUniqueVersion(representative.Version, usedVersions);

                var isActive = !activeAssigned
                    && activeSource is not null
                    && SameConfig(group.Key, new LegacyAiConfigKey(
                        activeSource.Provider,
                        activeSource.Model,
                        activeSource.Temperature,
                        activeSource.MaxTokens,
                        activeSource.ApiUrl,
                        activeSource.ApiKey));

                dbContext.AiConfigs.Add(new Infrastructure.Entities.System.AiConfig
                {
                    Name = created == 0
                        ? "Default AI Config"
                        : $"Legacy AI Config {created + 1}",
                    Provider = representative.Provider.ToString(),
                    Model = representative.Model,
                    Temperature = representative.Temperature,
                    MaxTokens = representative.MaxTokens,
                    ApiUrl = representative.ApiUrl,
                    ApiKey = _aiSecretProtector.Protect(representative.ApiKey),
                    Version = normalizedVersion,
                    IsActive = isActive,
                    CreatedAt = representative.CreatedAt,
                    UpdatedAt = representative.UpdatedAt
                });

                activeAssigned |= isActive;
                created++;
            }

            if (!activeAssigned && dbContext.ChangeTracker.Entries<Infrastructure.Entities.System.AiConfig>().Any())
            {
                dbContext.ChangeTracker.Entries<Infrastructure.Entities.System.AiConfig>()
                    .OrderByDescending(entry => entry.Entity.CreatedAt)
                    .First()
                    .Entity.IsActive = true;
            }

            if (created > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Backfilled {count} AI config version(s) from legacy prompt-level config.", created);
            }

            await EncryptLegacyAiConfigSecretsAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI config backfill failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EncryptLegacyAiConfigSecretsAsync(ResQDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!_aiSecretProtector.HasActiveKey)
        {
            _logger.LogWarning("AI secret encryption skipped because the master key is not configured.");
            return;
        }

        var configs = await dbContext.AiConfigs
            .Where(config => config.ApiKey != null && config.ApiKey != string.Empty)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var config in configs)
        {
            if (_aiSecretProtector.IsProtected(config.ApiKey))
            {
                continue;
            }

            config.ApiKey = _aiSecretProtector.Protect(config.ApiKey);
            config.UpdatedAt = DateTime.UtcNow;
            updated++;
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Encrypted {count} legacy AI config API key(s) in the database.", updated);
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @tableName
            );
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true || (result is bool exists && exists);
    }

    private static async Task<bool> LegacyPromptConfigColumnsExistAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'prompts'
              AND column_name IN ('provider', 'model', 'temperature', 'max_tokens', 'api_url', 'api_key');
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 6;
    }

    private static async Task<List<LegacyPromptConfigRow>> ReadLegacyPromptConfigsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, prompt_type, provider, model, temperature, max_tokens, api_url, api_key, version, is_active, created_at, updated_at
            FROM prompts
            WHERE provider IS NOT NULL
              AND model IS NOT NULL
              AND temperature IS NOT NULL
              AND max_tokens IS NOT NULL
              AND api_url IS NOT NULL;
            """;

        var rows = new List<LegacyPromptConfigRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new LegacyPromptConfigRow
            {
                PromptId = reader.GetInt32(0),
                PromptType = Enum.TryParse<PromptType>(reader.GetString(1), true, out var promptType)
                    ? promptType
                    : PromptType.MissionPlanning,
                Provider = Enum.TryParse<AiProvider>(reader.GetString(2), true, out var provider)
                    ? provider
                    : AiProvider.Gemini,
                Model = reader.GetString(3),
                Temperature = reader.GetDouble(4),
                MaxTokens = reader.GetInt32(5),
                ApiUrl = reader.GetString(6),
                ApiKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                Version = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsActive = reader.GetBoolean(9),
                CreatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            });
        }

        return rows;
    }

    private static string CreateUniqueVersion(string? sourceVersion, ISet<string> usedVersions)
    {
        var baseVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(sourceVersion);
        if (usedVersions.Add(baseVersion))
        {
            return baseVersion;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseVersion}.{index}";
            if (usedVersions.Add(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static bool SameConfig(LegacyAiConfigKey left, LegacyAiConfigKey right)
    {
        return left.Provider == right.Provider
               && string.Equals(left.Model, right.Model, StringComparison.OrdinalIgnoreCase)
               && left.Temperature.Equals(right.Temperature)
               && left.MaxTokens == right.MaxTokens
               && string.Equals(left.ApiUrl, right.ApiUrl, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.ApiKey, right.ApiKey, StringComparison.Ordinal);
    }

    private sealed record LegacyAiConfigKey(
        AiProvider Provider,
        string Model,
        double Temperature,
        int MaxTokens,
        string ApiUrl,
        string? ApiKey);

    private sealed class LegacyPromptConfigRow
    {
        public int PromptId { get; set; }
        public PromptType PromptType { get; set; }
        public AiProvider Provider { get; set; }
        public string Model { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public string ApiUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? Version { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
