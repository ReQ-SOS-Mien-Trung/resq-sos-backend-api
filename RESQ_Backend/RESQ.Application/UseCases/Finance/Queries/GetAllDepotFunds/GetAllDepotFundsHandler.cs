using MediatR;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

/// <summary>
/// Handler: Lấy tất cả quỹ kho kèm thông tin depot.
/// </summary>
public class GetAllDepotFundsHandler : IRequestHandler<GetAllDepotFundsQuery, List<DepotFundListItemDto>>
{
    private readonly IDepotFundRepository _depotFundRepo;

    public GetAllDepotFundsHandler(IDepotFundRepository depotFundRepo)
    {
        _depotFundRepo = depotFundRepo;
    }

    public async Task<List<DepotFundListItemDto>> Handle(GetAllDepotFundsQuery request, CancellationToken cancellationToken)
    {
        var funds = await _depotFundRepo.GetAllWithDepotInfoAsync(cancellationToken);

        return funds.Select(f => new DepotFundListItemDto
        {
            Id = f.Id,
            DepotId = f.DepotId,
            DepotName = f.DepotName,
            Balance = f.Balance,
            AdvanceLimit = f.AdvanceLimit,
            OutstandingAdvanceAmount = f.OutstandingAdvanceAmount,
            FundSourceType = f.FundSourceType,
            FundSourceName = f.FundSourceName,
            LastUpdatedAt = f.LastUpdatedAt == DateTime.MinValue ? null : f.LastUpdatedAt
        }).ToList();
    }
}
