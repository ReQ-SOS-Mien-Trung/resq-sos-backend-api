using MediatR;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

public class GetAllDepotFundsHandler : IRequestHandler<GetAllDepotFundsQuery, List<DepotFundsResponseDto>>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;

    public GetAllDepotFundsHandler(IDepotFundRepository depotFundRepo, IDepotRepository depotRepo)
    {
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
    }

    public async Task<List<DepotFundsResponseDto>> Handle(GetAllDepotFundsQuery request, CancellationToken cancellationToken)
    {
        var funds = await _depotFundRepo.GetAllWithDepotInfoAsync(cancellationToken);
        var depots = (await _depotRepo.GetAllAsync(cancellationToken)).ToDictionary(d => d.Id);

        var grouped = funds.GroupBy(f => f.DepotId).Select(g =>
        {
            var depotId = g.Key;
            var depot = depots.TryGetValue(depotId, out var d) ? d : null;

            return new DepotFundsResponseDto
            {
                DepotId = depotId,
                DepotName = g.First().DepotName,
                AdvanceLimit = depot?.AdvanceLimit ?? 0,
                OutstandingAdvanceAmount = depot?.OutstandingAdvanceAmount ?? 0,
                Funds = g.Select(f => new DepotFundItemDto
                {
                    Id = f.Id,
                    Balance = f.Balance,
                    FundSourceType = f.FundSourceType,
                    FundSourceName = f.FundSourceName,
                    LastUpdatedAt = f.LastUpdatedAt == DateTime.MinValue ? null : f.LastUpdatedAt
                }).OrderBy(f => f.Id).ToList()
            };
        }).OrderBy(g => g.DepotId).ToList();

        return grouped;
    }
}
