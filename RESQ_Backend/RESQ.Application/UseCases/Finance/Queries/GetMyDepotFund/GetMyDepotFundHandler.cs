using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// Handler: Lấy depot mà user đang quản lý → trả về danh sách TẤT CẢ quỹ của kho đó.
/// Mỗi quỹ có Id riêng — dùng để chọn khi gọi POST /logistics/inventory/import-purchase.
/// </summary>
public class GetMyDepotFundHandler : IRequestHandler<GetMyDepotFundQuery, List<DepotFundListItemDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IDepotFundRepository _depotFundRepo;

    public GetMyDepotFundHandler(
        IDepotInventoryRepository depotInventoryRepo,
        IDepotFundRepository depotFundRepo)
    {
        _depotInventoryRepo = depotInventoryRepo;
        _depotFundRepo = depotFundRepo;
    }

    public async Task<List<DepotFundListItemDto>> Handle(GetMyDepotFundQuery request, CancellationToken cancellationToken)
    {
        // 1. Lấy depot mà user đang quản lý
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        // 2. Lấy TẤT CẢ quỹ của kho (nhiều nguồn: Campaign A, Campaign B, SystemFund, ...)
        var funds = await _depotFundRepo.GetAllByDepotIdAsync(depotId, cancellationToken);

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
