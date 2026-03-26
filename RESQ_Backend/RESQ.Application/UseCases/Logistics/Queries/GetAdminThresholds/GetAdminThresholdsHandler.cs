using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAdminThresholds;

public class GetAdminThresholdsHandler(
    IStockThresholdConfigRepository stockThresholdConfigRepository)
    : IRequestHandler<GetAdminThresholdsQuery, GetAdminThresholdsResponse>
{
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;

    public async Task<GetAdminThresholdsResponse> Handle(GetAdminThresholdsQuery request, CancellationToken cancellationToken)
    {
        var global = await _stockThresholdConfigRepository.GetActiveGlobalAsync(cancellationToken);

        var response = new GetAdminThresholdsResponse
        {
            Global = global != null ? Map(global) : null,
            DepotId = request.DepotId
        };

        if (request.DepotId.HasValue)
        {
            var scoped = await _stockThresholdConfigRepository.GetActiveDepotScopedConfigsAsync(request.DepotId.Value, cancellationToken);

            response.Depot = scoped
                .Where(x => x.ScopeType == StockThresholdScopeType.Depot)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(Map)
                .FirstOrDefault();

            response.DepotCategories = scoped
                .Where(x => x.ScopeType == StockThresholdScopeType.DepotCategory)
                .OrderBy(x => x.CategoryId)
                .Select(Map)
                .ToList();

            response.DepotItems = scoped
                .Where(x => x.ScopeType == StockThresholdScopeType.DepotItem)
                .OrderBy(x => x.ItemModelId)
                .Select(Map)
                .ToList();
        }

        return response;
    }

    private static ThresholdConfigDto Map(Thresholds.StockThresholdConfigDto x)
        => new()
        {
            Id = x.Id,
            ScopeType = x.ScopeType.ToString(),
            CategoryId = x.CategoryId,
            ItemModelId = x.ItemModelId,
            DangerPercent = x.DangerRatio * 100m,
            WarningPercent = x.WarningRatio * 100m,
            RowVersion = x.RowVersion,
            UpdatedAt = x.UpdatedAt
        };
}
