using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetMyDepotLowStockHandler(IDepotInventoryRepository depotInventoryRepo)
    : IRequestHandler<GetMyDepotLowStockQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;

    public async Task<LowStockChartResponseDto> Handle(
        GetMyDepotLowStockQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var items = await _depotInventoryRepo.GetLowStockItemsAsync(depotId, request.AlertLevel, cancellationToken);
        return LowStockChartBuilder.Build(items);
    }
}
