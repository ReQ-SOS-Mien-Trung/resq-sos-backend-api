using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

public class GetMyDepotFundHandler : IRequestHandler<GetMyDepotFundQuery, MyDepotFundsResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;

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
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tai khoan hien tai không du?c ch? d?nh qu?n lý bat ky kho nao dang hoat dong.");

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

