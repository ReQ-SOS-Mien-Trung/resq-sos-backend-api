using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLots;

public class GetInventoryLotsQueryHandler(IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetInventoryLotsQuery, PagedResult<InventoryLotDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<PagedResult<InventoryLotDto>> Handle(GetInventoryLotsQuery request, CancellationToken cancellationToken)
    {
        var depotId = request.DepotId
            ?? await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ được chỉ định.");

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
