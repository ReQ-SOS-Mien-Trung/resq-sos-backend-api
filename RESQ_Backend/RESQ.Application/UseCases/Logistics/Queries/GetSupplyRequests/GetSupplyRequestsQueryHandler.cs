using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class GetSupplyRequestsQueryHandler(
    IDepotInventoryRepository depotInventoryRepository,
    ISupplyRequestRepository  supplyRequestRepository,
    ILogger<GetSupplyRequestsQueryHandler> logger)
    : IRequestHandler<GetSupplyRequestsQuery, GetSupplyRequestsResponse>
{
    private readonly IDepotInventoryRepository     _depotInventoryRepository = depotInventoryRepository;
    private readonly ISupplyRequestRepository      _supplyRequestRepository  = supplyRequestRepository;
    private readonly ILogger<GetSupplyRequestsQueryHandler> _logger          = logger;

    public async Task<GetSupplyRequestsResponse> Handle(GetSupplyRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSupplyRequestsQuery for UserId={UserId}", request.UserId);

        var depotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(request.UserId, cancellationToken);
        if (depotIds.Count == 0)
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var paged = await _supplyRequestRepository.GetPagedByDepotsAsync(
            depotIds,
            request.SourceStatus?.ToString(),
            request.RequestingStatus?.ToString(),
            request.RoleFilter?.ToString(),
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        // Snapshot một lần để RemainingSeconds nhất quán trong toàn bộ response
        var nowUtc = DateTime.UtcNow;
        const string autoRejectReason = "Hệ thống tự động từ chối do quá thời gian phản hồi.";

        // Eagerly auto-reject any expired pending requests so the caller sees the correct status
        // without waiting for the background service (which runs every 1 minute).
        foreach (var item in paged.Items)
        {
            if (item.SourceStatus == "Pending"
                && item.RequestingStatus == "WaitingForApproval"
                && item.AutoRejectAt.HasValue
                && item.AutoRejectAt.Value <= nowUtc)
            {
                var rejected = await _supplyRequestRepository.AutoRejectIfPendingAsync(
                    item.Id, autoRejectReason, cancellationToken);
                if (rejected)
                {
                    item.SourceStatus      = "Rejected";
                    item.RequestingStatus  = "Rejected";
                    item.RejectedReason    = autoRejectReason;
                }
            }
        }

        var dtos = paged.Items.Select(item =>
        {
            // Null guard: AutoRejectAt phải luôn có giá trị với data hợp lệ
            DateTime deadlineUtc;
            if (item.AutoRejectAt.HasValue)
            {
                deadlineUtc = item.AutoRejectAt.Value;
            }
            else
            {
                _logger.LogWarning(
                    "SupplyRequest {Id} has null AutoRejectAt — falling back to CreatedAt + default MediumMinutes ({Minutes} min)",
                    item.Id,
                    SupplyRequestPriorityPolicy.DefaultTiming.MediumMinutes);
                deadlineUtc = item.CreatedAt.AddMinutes(SupplyRequestPriorityPolicy.DefaultTiming.MediumMinutes);
            }

            return new SupplyRequestDto
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
                ResponseDeadline    = deadlineUtc.ToVietnamOffset(),
                RemainingSeconds    = Math.Max(0, (long)Math.Ceiling((deadlineUtc - nowUtc).TotalSeconds)),
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
            };
        }).ToList();

        var pagedDtos = new PagedResult<SupplyRequestDto>(dtos, paged.TotalCount, request.PageNumber, request.PageSize);

        return new GetSupplyRequestsResponse
        {
            Data       = pagedDtos,
            ServerTime = nowUtc.ToVietnamOffset()
        };
    }

}
