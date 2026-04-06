using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;

public class GetDepotClosuresQueryHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    ILogger<GetDepotClosuresQueryHandler> logger)
    : IRequestHandler<GetDepotClosuresQuery, List<DepotClosureDto>>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotClosureRepository _closureRepository = closureRepository;
    private readonly ILogger<GetDepotClosuresQueryHandler> _logger = logger;

    public async Task<List<DepotClosureDto>> Handle(GetDepotClosuresQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Handler} for DepotId={DepotId}", nameof(GetDepotClosuresQueryHandler), request.DepotId);

        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
            throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var items = await _closureRepository.GetClosuresByDepotIdAsync(request.DepotId, cancellationToken);

        var result = items.Select(item => new DepotClosureDto
        {
            Id                      = item.Id,
            DepotId                 = item.DepotId,
            Status                  = item.Status.ToString(),
            PreviousStatus          = item.PreviousStatus.ToString(),
            CloseReason             = item.CloseReason,
            ResolutionType          = item.ResolutionType?.ToString(),
            TargetDepotId           = item.TargetDepotId,
            TargetDepotName         = item.TargetDepotName,
            ExternalNote            = item.ExternalNote,
            InitiatedBy             = item.InitiatedBy,
            InitiatedByFullName     = item.InitiatedByFullName,
            CancelledBy             = item.CancelledBy,
            CancelledByFullName     = item.CancelledByFullName,
            CancellationReason      = item.CancellationReason,
            SnapshotConsumableUnits = item.SnapshotConsumableUnits,
            SnapshotReusableUnits   = item.SnapshotReusableUnits,
            InitiatedAt             = item.InitiatedAt,
            ClosingTimeoutAt        = item.ClosingTimeoutAt,
            CompletedAt             = item.CompletedAt,
            CancelledAt             = item.CancelledAt,
            Transfer                = item.TransferId.HasValue
                ? new TransferSummaryDto
                {
                    TransferId = item.TransferId.Value,
                    Status     = item.TransferStatus ?? string.Empty
                }
                : null
        }).ToList();

        _logger.LogInformation("{Handler} returned {Count} closure(s) for DepotId={DepotId}",
            nameof(GetDepotClosuresQueryHandler), result.Count, request.DepotId);

        return result;
    }
}
