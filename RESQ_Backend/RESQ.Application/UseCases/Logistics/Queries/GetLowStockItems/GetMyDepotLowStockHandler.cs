using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetMyDepotLowStockHandler(
    IDepotInventoryRepository depotInventoryRepo,
    IStockThresholdResolver stockThresholdResolver)
    : IRequestHandler<GetMyDepotLowStockQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly IStockThresholdResolver _stockThresholdResolver = stockThresholdResolver;

    public async Task<LowStockChartResponseDto> Handle(
        GetMyDepotLowStockQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var rawItems = await _depotInventoryRepo.GetLowStockRawItemsAsync(depotId, cancellationToken);
        var items = new List<LowStockItemDto>();

        foreach (var raw in rawItems)
        {
            var threshold = await _stockThresholdResolver.ResolveAsync(raw.DepotId, raw.CategoryId, raw.ItemModelId, cancellationToken);
            var level = StockLevelClassifier.Classify(raw.AvailableQuantity, raw.Quantity, threshold);

            if (level is not (StockLevel.Warning or StockLevel.Danger))
                continue;

            if (request.AlertLevel.HasValue)
            {
                var expected = request.AlertLevel.Value == StockAlertLevel.Danger
                    ? StockLevel.Danger
                    : StockLevel.Warning;
                if (level != expected)
                    continue;
            }

            items.Add(new LowStockItemDto
            {
                DepotId = raw.DepotId,
                DepotName = raw.DepotName,
                ItemModelId = raw.ItemModelId,
                ItemModelName = raw.ItemModelName,
                Unit = raw.Unit,
                CategoryId = raw.CategoryId,
                CategoryName = raw.CategoryName,
                TargetGroup = raw.TargetGroup,
                Quantity = raw.Quantity,
                ReservedQuantity = raw.ReservedQuantity,
                AvailableQuantity = raw.AvailableQuantity,
                AvailableRatio = raw.Quantity <= 0 ? 0 : Math.Round((double)raw.AvailableQuantity / raw.Quantity, 4),
                AlertLevel = level.ToString(),
                AlertLevelLabel = level == StockLevel.Danger ? "Nguy hiểm" : "Cảnh báo"
            });
        }

        items = items.OrderBy(x => x.AvailableRatio).ThenBy(x => x.DepotId).ToList();
        return LowStockChartBuilder.Build(items);
    }
}
