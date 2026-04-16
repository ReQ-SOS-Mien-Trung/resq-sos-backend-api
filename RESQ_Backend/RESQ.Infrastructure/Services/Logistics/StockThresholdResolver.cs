using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Thresholds;
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

    public Task InvalidateDepotScopeAsync(int depotId)
    {
        _memoryCache.Remove(GetDepotKey(depotId));
        return Task.CompletedTask;
    }

    public Task InvalidateGlobalAsync()
    {
        _memoryCache.Remove("stock-threshold:global");
        return Task.CompletedTask;
    }

    public async Task<(int? Value, ThresholdResolutionScope Scope)> ResolveMinimumThresholdAsync(
        int depotId,
        int? categoryId,
        int itemModelId,
        CancellationToken cancellationToken = default)
    {
        var depotConfigs = await GetDepotConfigsAsync(depotId, cancellationToken);

        var candidates = new List<(StockThresholdConfigDto? Config, ThresholdResolutionScope Scope)>
        {
            (depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.DepotItem && x.ItemModelId == itemModelId),
             ThresholdResolutionScope.Item),
            (categoryId.HasValue
                ? depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.DepotCategory && x.CategoryId == categoryId.Value)
                : null,
             ThresholdResolutionScope.Category),
            (depotConfigs.FirstOrDefault(x => x.ScopeType == StockThresholdScopeType.Depot),
             ThresholdResolutionScope.Depot)
        };

        foreach (var (config, scope) in candidates)
        {
            if (config?.MinimumThreshold is > 0)
                return (config.MinimumThreshold, scope);
        }

        var global = await GetGlobalConfigAsync(cancellationToken);
        if (global?.MinimumThreshold is > 0)
            return (global.MinimumThreshold, ThresholdResolutionScope.Global);

        return (null, ThresholdResolutionScope.None);
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
