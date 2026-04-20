using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;

public class GetMyDepotItemModelAlertsQueryHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetMyDepotItemModelAlertsQuery, PagedResult<DepotItemModelAlertDto>>
{
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<PagedResult<DepotItemModelAlertDto>> Handle(
        GetMyDepotItemModelAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(
            request.UserId,
            request.DepotId,
            cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var now = DateTime.UtcNow.Date;
        var expiringCandidates = await _depotInventoryRepository.GetExpiringItemModelAlertCandidatesAsync(depotId, cancellationToken);
        var maintenanceCandidates = await _depotInventoryRepository.GetMaintenanceItemModelAlertCandidatesAsync(depotId, cancellationToken);
        var alerts = DepotItemModelAlertFactory.BuildAll(expiringCandidates, maintenanceCandidates, now);

        if (!string.IsNullOrWhiteSpace(request.AlertType))
        {
            alerts = alerts
                .Where(x => string.Equals(x.AlertType, request.AlertType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var safePageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var safePageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var pagedItems = alerts
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new PagedResult<DepotItemModelAlertDto>(pagedItems, alerts.Count, safePageNumber, safePageSize);
    }
}
