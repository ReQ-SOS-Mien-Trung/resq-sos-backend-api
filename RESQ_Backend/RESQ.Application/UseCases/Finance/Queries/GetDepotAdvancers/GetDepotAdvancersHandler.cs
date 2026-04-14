using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotAdvancers;

public class GetDepotAdvancersHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepo,
    IDepotFundRepository depotFundRepo)
    : IRequestHandler<GetDepotAdvancersQuery, PagedResult<DepotAdvancerDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;

    public async Task<PagedResult<DepotAdvancerDto>> Handle(
        GetDepotAdvancersQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại chưa được phân công quản lý kho đang hoạt động.");

        var pagedDebts = await _depotFundRepo.GetPagedAdvancersByDepotIdAsync(
            depotId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedDebts.Items.Select(debt =>
        {
            var outstanding = Math.Max(0m, debt.TotalAdvancedAmount - debt.TotalRepaidAmount);
            var repaidPercentage = debt.TotalAdvancedAmount <= 0m
                ? 100m
                : Math.Min(100m, Math.Round(debt.TotalRepaidAmount / debt.TotalAdvancedAmount * 100m, 2, MidpointRounding.AwayFromZero));

            return new DepotAdvancerDto
            {
                ContributorName = debt.ContributorName,
                ContributorPhoneNumber = debt.ContributorPhoneNumber,
                TotalAdvancedAmount = debt.TotalAdvancedAmount,
                TotalRepaidAmount = debt.TotalRepaidAmount,
                OutstandingAmount = outstanding,
                RepaidPercentage = repaidPercentage
            };
        }).ToList();

        return new PagedResult<DepotAdvancerDto>(
            dtos,
            pagedDebts.TotalCount,
            pagedDebts.PageNumber,
            pagedDebts.PageSize);
    }
}
