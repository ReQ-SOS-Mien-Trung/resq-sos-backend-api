using Microsoft.Extensions.Caching.Memory;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Infrastructure.Services.Logistics;

public class StockWarningEvaluatorService(
    IStockWarningBandConfigRepository bandConfigRepository,
    IStockThresholdResolver thresholdResolver,
    IMemoryCache memoryCache) : IStockWarningEvaluatorService
{
    private readonly IStockWarningBandConfigRepository _bandConfigRepository = bandConfigRepository;
    private readonly IStockThresholdResolver _thresholdResolver = thresholdResolver;
    private readonly IMemoryCache _memoryCache = memoryCache;

    private const string BandCacheKey = "stock-warning:bands";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<StockWarningResult> EvaluateAsync(
        int depotId,
        int? categoryId,
        int itemModelId,
        int availableQty,
        CancellationToken cancellationToken = default)
    {
        var bandSet = await GetBandSetAsync(cancellationToken);
        if (bandSet == null)
            return StockWarningResult.Unconfigured;

        var (threshold, scope) = await _thresholdResolver.ResolveMinimumThresholdAsync(
            depotId, categoryId, itemModelId, cancellationToken);

        return StockWarningEvaluator.Evaluate(availableQty, threshold, bandSet, scope);
    }

    public Task InvalidateBandCacheAsync()
    {
        _memoryCache.Remove(BandCacheKey);
        return Task.CompletedTask;
    }

    private async Task<WarningBandSet?> GetBandSetAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(BandCacheKey, out WarningBandSet? cached) && cached != null)
            return cached;

        var config = await _bandConfigRepository.GetAsync(cancellationToken);
        if (config == null || config.Bands.Count == 0)
            return null;

        var domainBands = config.Bands
            .Select(b => new WarningBand(b.Name, b.From, b.To))
            .ToList();

        var bandSet = new WarningBandSet(domainBands);
        _memoryCache.Set(BandCacheKey, bandSet, CacheTtl);
        return bandSet;
    }
}
