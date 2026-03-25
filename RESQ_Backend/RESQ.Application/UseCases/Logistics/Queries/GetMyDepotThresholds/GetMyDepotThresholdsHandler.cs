using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;

public class GetMyDepotThresholdsHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IStockThresholdConfigRepository stockThresholdConfigRepository)
    : IRequestHandler<GetMyDepotThresholdsQuery, GetMyDepotThresholdsResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;

    public async Task<GetMyDepotThresholdsResponse> Handle(GetMyDepotThresholdsQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var global = await _stockThresholdConfigRepository.GetActiveGlobalAsync(cancellationToken);
        var scoped = await _stockThresholdConfigRepository.GetActiveDepotScopedConfigsAsync(depotId, cancellationToken);

        return new GetMyDepotThresholdsResponse
        {
            DepotId = depotId,
            Global = global != null ? Map(global) : null,
            Depot = scoped.Where(x => x.ScopeType == StockThresholdScopeType.Depot)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(Map)
                .FirstOrDefault(),
            DepotCategories = scoped.Where(x => x.ScopeType == StockThresholdScopeType.DepotCategory)
                .OrderBy(x => x.CategoryId)
                .Select(Map)
                .ToList(),
            DepotItems = scoped.Where(x => x.ScopeType == StockThresholdScopeType.DepotItem)
                .OrderBy(x => x.ItemModelId)
                .Select(Map)
                .ToList()
        };
    }

    private static ThresholdConfigDto Map(UseCases.Logistics.Thresholds.StockThresholdConfigDto x)
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
