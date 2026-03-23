using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetLowStockItemsHandler(IDepotInventoryRepository depotInventoryRepo)
    : IRequestHandler<GetLowStockItemsQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;

    public async Task<LowStockChartResponseDto> Handle(
        GetLowStockItemsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _depotInventoryRepo.GetLowStockItemsAsync(request.DepotId, request.AlertLevel, cancellationToken);
        return LowStockChartBuilder.Build(items);
    }
}
