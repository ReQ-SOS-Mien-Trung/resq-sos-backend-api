using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

public class GetMyDepotFundHandler : IRequestHandler<GetMyDepotFundQuery, MyDepotFundsResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotRepository _depotRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public GetMyDepotFundHandler(
        IDepotInventoryRepository depotInventoryRepo,
        IDepotFundRepository depotFundRepo,
        IDepotRepository depotRepo)
    {
        _depotInventoryRepo = depotInventoryRepo;
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
    }

    public async Task<MyDepotFundsResponseDto> Handle(GetMyDepotFundQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var funds = await _depotFundRepo.GetAllByDepotIdAsync(depotId, cancellationToken);
        var depot = await _depotRepo.GetByIdAsync(depotId, cancellationToken);

        return new MyDepotFundsResponseDto
        {
            AdvanceLimit = depot?.AdvanceLimit ?? 0,
            OutstandingAdvanceAmount = depot?.OutstandingAdvanceAmount ?? 0,
            Funds = funds.OrderBy(f => f.Id).Select(f => new MyDepotFundItemDto
            {
                Id = f.Id,
                DepotId = f.DepotId,
                DepotName = f.DepotName,
                Balance = f.Balance,
                FundSourceType = f.FundSourceType,
                FundSourceName = f.FundSourceName,
                LastUpdatedAt = f.LastUpdatedAt == DateTime.MinValue ? null : f.LastUpdatedAt
            }).ToList()
        };
    }
}
