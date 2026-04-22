using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetExternalClosureResolutionState;

public class GetExternalClosureResolutionStateQueryHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository)
    : IRequestHandler<GetExternalClosureResolutionStateQuery, ExternalClosureResolutionStateResponse>
{
    public async Task<ExternalClosureResolutionStateResponse> Handle(
        GetExternalClosureResolutionStateQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.RequestingUserId,
                request.DepotId,
                cancellationToken)
            ?? throw new ForbiddenException("Bạn không có quyền thao tác với kho này.");

        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var closure = await closureRepository.GetActiveClosureByDepotIdAsync(depotId, cancellationToken);
        if (closure == null)
        {
            return new ExternalClosureResolutionStateResponse
            {
                DepotId = depotId,
                HasActiveExternalResolution = false
            };
        }

        var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(closure.Id, cancellationToken);
        var remainingItems = await depotRepository.GetDetailedInventoryForClosureAsync(depotId, cancellationToken);

        var canHandleExternalResolution = depot.Status == DepotStatus.Closing
                                          && closure.Status == DepotClosureStatus.InProgress
                                          && closure.ResolutionType == CloseResolutionType.ExternalResolution
                                          && remainingItems.Count > 0
                                          && !hasOpenTransfers;

        return new ExternalClosureResolutionStateResponse
        {
            DepotId = depotId,
            ClosureId = closure.Id,
            HasActiveExternalResolution = closure.ResolutionType == CloseResolutionType.ExternalResolution,
            CanDownloadExternalTemplate = canHandleExternalResolution,
            CanUploadExternalResolution = canHandleExternalResolution,
            ClosureStatus = closure.Status.ToString(),
            ResolutionType = closure.ResolutionType?.ToString(),
            ExternalNote = closure.ExternalNote,
            RemainingItemCount = remainingItems.Count
        };
    }
}
