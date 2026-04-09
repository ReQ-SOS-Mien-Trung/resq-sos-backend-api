using MediatR;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundsByDepotId;

/// <summary>
/// Handler: Lấy tất cả quỹ kho theo depot ID (cho Admin xem chi tiết từng kho).
/// Trả về danh sách tất cả DepotFund records — mỗi quỹ gắn với 1 nguồn (Campaign / SystemFund).
/// </summary>
public class GetDepotFundsByDepotIdHandler : IRequestHandler<GetDepotFundsByDepotIdQuery, List<DepotFundListItemDto>>
{
    private readonly IDepotFundRepository _depotFundRepo;

    public GetDepotFundsByDepotIdHandler(IDepotFundRepository depotFundRepo)
    {
        _depotFundRepo = depotFundRepo;
    }

    public async Task<List<DepotFundListItemDto>> Handle(GetDepotFundsByDepotIdQuery request, CancellationToken cancellationToken)
    {
        var funds = await _depotFundRepo.GetAllByDepotIdAsync(request.DepotId, cancellationToken);

        return funds.Select(f => new DepotFundListItemDto
        {
            Id             = f.Id,
            DepotId        = f.DepotId,
            DepotName      = f.DepotName,
            Balance        = f.Balance,
            MaxAdvanceLimit = f.MaxAdvanceLimit,
            FundSourceType = f.FundSourceType,
            FundSourceName = f.FundSourceName,
            LastUpdatedAt  = f.LastUpdatedAt == DateTime.MinValue ? null : f.LastUpdatedAt
        }).ToList();
    }
}
