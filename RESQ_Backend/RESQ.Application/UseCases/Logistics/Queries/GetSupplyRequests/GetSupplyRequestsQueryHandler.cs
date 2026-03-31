using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class GetSupplyRequestsQueryHandler(
    IDepotInventoryRepository depotInventoryRepository,
    ISupplyRequestRepository  supplyRequestRepository,
    ILogger<GetSupplyRequestsQueryHandler> logger)
    : IRequestHandler<GetSupplyRequestsQuery, PagedResult<SupplyRequestDto>>
{
    private readonly IDepotInventoryRepository     _depotInventoryRepository = depotInventoryRepository;
    private readonly ISupplyRequestRepository      _supplyRequestRepository  = supplyRequestRepository;
    private readonly ILogger<GetSupplyRequestsQueryHandler> _logger          = logger;

    public async Task<PagedResult<SupplyRequestDto>> Handle(GetSupplyRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSupplyRequestsQuery for UserId={UserId}", request.UserId);

        var depotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(request.UserId, cancellationToken);
        if (depotIds.Count == 0)
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var paged = await _supplyRequestRepository.GetPagedByDepotsAsync(
            depotIds,
            request.SourceStatus?.ToString(),
            request.RequestingStatus?.ToString(),
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = paged.Items.Select(item => new SupplyRequestDto
        {
            Id                  = item.Id,
            RequestingDepotId   = item.RequestingDepotId,
            RequestingDepotName = item.RequestingDepotName,
            SourceDepotId       = item.SourceDepotId,
            SourceDepotName     = item.SourceDepotName,
            PriorityLevel       = item.PriorityLevel,
            SourceStatus        = item.SourceStatus,
            RequestingStatus    = item.RequestingStatus,
            Note                = item.Note,
            RejectedReason      = item.RejectedReason,
            RequestedBy         = item.RequestedBy,
            CreatedAt           = item.CreatedAt,
            AutoRejectAt        = item.AutoRejectAt,
            RespondedAt         = item.RespondedAt,
            ShippedAt           = item.ShippedAt,
            CompletedAt         = item.CompletedAt,
            Role                = depotIds.Contains(item.RequestingDepotId) ? "Requester" : "Source",
            Items               = item.Items.Select(i => new SupplyRequestItemDto
            {
                ItemModelId   = i.ItemModelId,
                ItemModelName = i.ItemModelName,
                Unit           = i.Unit,
                Quantity       = i.Quantity
            }).ToList()
        }).ToList();

        return new PagedResult<SupplyRequestDto>(dtos, paged.TotalCount, request.PageNumber, request.PageSize);
    }
}
