using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetMyDepotLowStockHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepo,
    IStockWarningEvaluatorService evaluatorService)
    : IRequestHandler<GetMyDepotLowStockQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IStockWarningEvaluatorService _evaluatorService = evaluatorService;

    public async Task<LowStockChartResponseDto> Handle(
        GetMyDepotLowStockQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var rawItems = await _depotInventoryRepo.GetLowStockRawItemsAsync(
            depotId,
            request.CategoryIds,
            cancellationToken);
        var items = new List<LowStockItemDto>();

        foreach (var raw in rawItems)
        {
            var result = await _evaluatorService.EvaluateAsync(
                raw.DepotId, raw.CategoryId, raw.ItemModelId, raw.AvailableQuantity, cancellationToken);

            // Bỏ qua vật phẩm đang OK
            if (result.Level == StockWarningLevel.Ok)
                continue;

            // Bỏ qua UNCONFIGURED nếu không yêu cầu
            if (result.Level == StockWarningLevel.Unconfigured && !request.IncludeUnconfigured)
                continue;

            // Lọc theo level nếu có
            if (request.WarningLevel != null &&
                !string.Equals(result.Level, request.WarningLevel, StringComparison.OrdinalIgnoreCase))
                continue;

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
                MinimumThreshold = result.ResolvedThreshold,
                SeverityRatio = result.SeverityRatio,
                WarningLevel = result.Level,
                ResolvedThresholdScope = result.ResolvedScope.ToString(),
                IsUsingGlobalDefault = result.IsUsingGlobalDefault
            });
        }

        items = items
            .OrderBy(x => x.SeverityRatio)
            .ThenBy(x => x.DepotId)
            .ToList();

        return LowStockChartBuilder.Build(items, request.PageNumber, request.PageSize);
    }
}

