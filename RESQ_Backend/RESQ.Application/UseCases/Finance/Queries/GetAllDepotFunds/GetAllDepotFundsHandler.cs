using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

public class GetAllDepotFundsHandler : IRequestHandler<GetAllDepotFundsQuery, PagedResult<DepotFundsResponseDto>>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;

    public GetAllDepotFundsHandler(IDepotFundRepository depotFundRepo, IDepotRepository depotRepo)
    {
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
    }

    public async Task<PagedResult<DepotFundsResponseDto>> Handle(GetAllDepotFundsQuery request, CancellationToken cancellationToken)
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
        }).OrderBy(g => g.DepotId);

        // Lọc theo tên kho nếu có
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            grouped = grouped.Where(g => g.DepotName != null && g.DepotName.ToLower().Contains(keyword))
                             .OrderBy(g => g.DepotId);
        }

        var allItems = grouped.ToList();
        var totalCount = allItems.Count;
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var pagedItems = allItems.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<DepotFundsResponseDto>(pagedItems, totalCount, pageNumber, pageSize);
    }
}
