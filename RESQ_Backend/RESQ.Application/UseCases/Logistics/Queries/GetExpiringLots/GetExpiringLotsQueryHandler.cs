using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetExpiringLots;

public class GetExpiringLotsQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetExpiringLotsQuery, List<ExpiringLotDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<List<ExpiringLotDto>> Handle(GetExpiringLotsQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw ExceptionCodes.WithCode(
                new NotFoundException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                LogisticsErrorCodes.DepotManagerNotAssigned);

        var lots = await _depotInventoryRepository.GetExpiringLotsAsync(depotId, request.DaysAhead, cancellationToken);

        var now = DateTime.UtcNow;
        return lots.Select(l => new ExpiringLotDto
        {
            LotId = l.LotId,
            ItemModelId = l.ItemModelId,
            ItemModelName = l.ItemModelName,
            RemainingQuantity = l.RemainingQuantity,
            ExpiredDate = l.ExpiredDate,
            ReceivedDate = l.ReceivedDate,
            SourceType = l.SourceType,
            IsExpired = l.ExpiredDate.HasValue && l.ExpiredDate.Value < now
        }).ToList();
    }
}
