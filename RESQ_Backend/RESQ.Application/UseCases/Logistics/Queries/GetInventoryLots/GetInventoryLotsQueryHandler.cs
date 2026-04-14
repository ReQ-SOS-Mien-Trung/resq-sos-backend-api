using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLots;

public class GetInventoryLotsQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetInventoryLotsQuery, PagedResult<InventoryLotDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<InventoryLotDto>> Handle(GetInventoryLotsQuery request, CancellationToken cancellationToken)
    {
        int depotId;
        if (request.DepotId.HasValue)
        {
            depotId = request.DepotId.Value;
        }
        else
        {
            depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
                ?? throw ExceptionCodes.WithCode(
                    new NotFoundException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
        }

        var pagedLots = await _depotInventoryRepository.GetInventoryLotsAsync(
            depotId, request.ItemModelId, request.PageNumber, request.PageSize, cancellationToken);

        var now = DateTime.UtcNow;
        var expiringThreshold = now.AddDays(30);

        var dtos = pagedLots.Items.Select(lot => new InventoryLotDto
        {
            LotId = lot.Id,
            Quantity = lot.Quantity,
            RemainingQuantity = lot.RemainingQuantity,
            ReceivedDate = lot.ReceivedDate,
            ExpiredDate = lot.ExpiredDate,
            SourceType = lot.SourceType,
            CreatedAt = lot.CreatedAt,
            IsExpired = lot.ExpiredDate.HasValue && lot.ExpiredDate.Value < now,
            IsExpiringSoon = lot.ExpiredDate.HasValue && lot.ExpiredDate.Value >= now && lot.ExpiredDate.Value <= expiringThreshold
        }).ToList();

        return new PagedResult<InventoryLotDto>(dtos, pagedLots.TotalCount, pagedLots.PageNumber, pagedLots.PageSize);
    }
}
