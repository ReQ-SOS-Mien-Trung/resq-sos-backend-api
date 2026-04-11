using MediatR;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundsByDepotId;     

public class GetDepotFundsByDepotIdHandler : IRequestHandler<GetDepotFundsByDepotIdQuery, DepotFundsResponseDto?>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;

    public GetDepotFundsByDepotIdHandler(IDepotFundRepository depotFundRepo, IDepotRepository depotRepo)
    {
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
    }

    public async Task<DepotFundsResponseDto?> Handle(GetDepotFundsByDepotIdQuery request, CancellationToken cancellationToken)
    {
        var depot = await _depotRepo.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
            return null;

        var funds = await _depotFundRepo.GetAllByDepotIdAsync(request.DepotId, cancellationToken);

        return new DepotFundsResponseDto
        {
            DepotId = depot.Id,
            DepotName = depot.Name,
            AdvanceLimit = depot.AdvanceLimit,
            OutstandingAdvanceAmount = depot.OutstandingAdvanceAmount,
            Funds = funds.Select(f => new DepotFundItemDto
            {
                Id             = f.Id,
                Balance        = f.Balance,
                FundSourceType = f.FundSourceType,
                FundSourceName = f.FundSourceName,
                LastUpdatedAt  = f.LastUpdatedAt == DateTime.MinValue ? null : f.LastUpdatedAt
            }).ToList()
        };
    }
}
