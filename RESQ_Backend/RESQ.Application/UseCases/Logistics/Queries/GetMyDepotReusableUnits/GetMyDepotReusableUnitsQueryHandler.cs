using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;

public class GetMyDepotReusableUnitsQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetMyDepotReusableUnitsQuery, PagedResult<ReusableUnitDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<ReusableUnitDto>> Handle(GetMyDepotReusableUnitsQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw ExceptionCodes.WithCode(
                new NotFoundException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                LogisticsErrorCodes.DepotManagerNotAssigned);

        var statusStrings = request.Statuses?.Select(s => s.ToString()).ToList();
        var conditionStrings = request.Conditions?.Select(c => c.ToString()).ToList();

        return await _depotInventoryRepository.GetReusableUnitsPagedAsync(
            depotId,
            request.ItemModelId,
            request.SerialNumber,
            statusStrings,
            conditionStrings,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
