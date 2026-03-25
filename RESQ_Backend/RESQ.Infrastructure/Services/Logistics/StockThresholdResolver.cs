using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services.Logistics;

public class StockThresholdResolver(
    IStockThresholdConfigRepository stockThresholdConfigRepository,
    IMemoryCache memoryCache,
    ILogger<StockThresholdResolver> logger) : IStockThresholdResolver
{
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<StockThresholdResolver> _logger = logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private const decimal DefaultDangerRatio = 0.2m;
    private const decimal DefaultWarningRatio = 0.4m;

    public async Task<StockThreshold> ResolveAsync(int depotId, int? categoryId, int itemModelId, CancellationToken cancellationToken = default)
    {
        var depotConfigs = await GetDepotConfigsAsync(depotId, cancellationToken);

        var candidates = new List<StockThresholdConfigDto?>
        {
            depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.DepotItem && x.ItemModelId == itemModelId),
            categoryId.HasValue
                ? depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.DepotCategory && x.CategoryId == categoryId.Value)
                : null,
            depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.Depot)
        };

        var global = await GetGlobalConfigAsync(cancellationToken);
        candidates.Add(global);

        foreach (var candidate in candidates.Where(x => x != null))
        {
            try
            {
                return new StockThreshold(candidate!.DangerRatio, candidate.WarningRatio);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Invalid stock-threshold config detected. Fallback next scope. ConfigId={ConfigId}, Scope={ScopeType}",
                    candidate!.Id,
                    candidate.ScopeType);
            }
        }

        _logger.LogWarning("No valid stock-threshold config found. Falling back to hard default {Danger}/{Warning}.",
            DefaultDangerRatio, DefaultWarningRatio);

        return new StockThreshold(DefaultDangerRatio, DefaultWarningRatio);
    }

    public Task InvalidateDepotScopeAsync(int depotId)
    {
        _memoryCache.Remove(GetDepotKey(depotId));
        return Task.CompletedTask;
    }

    private async Task<List<StockThresholdConfigDto>> GetDepotConfigsAsync(int depotId, CancellationToken cancellationToken)
    {
        var cacheKey = GetDepotKey(depotId);
        if (_memoryCache.TryGetValue(cacheKey, out List<StockThresholdConfigDto>? cached) && cached != null)
            return cached;

        var loaded = await _stockThresholdConfigRepository.GetActiveDepotScopedConfigsAsync(depotId, cancellationToken);
        _memoryCache.Set(cacheKey, loaded, CacheTtl);
        return loaded;
    }

    private async Task<StockThresholdConfigDto?> GetGlobalConfigAsync(CancellationToken cancellationToken)
    {
        const string key = "stock-threshold:global";
        if (_memoryCache.TryGetValue(key, out StockThresholdConfigDto? cached))
            return cached;

        var loaded = await _stockThresholdConfigRepository.GetActiveGlobalAsync(cancellationToken);
        _memoryCache.Set(key, loaded, CacheTtl);
        return loaded;
    }

    private static string GetDepotKey(int depotId) => $"stock-threshold:depot:{depotId}";
}
