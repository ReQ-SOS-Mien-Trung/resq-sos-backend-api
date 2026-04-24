using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface ICampaignDisbursementRepository
{
    Task<CampaignDisbursementModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    Task<PagedResult<CampaignDisbursementModel>> GetPagedAsync(
        int pageNumber, int pageSize,
        int? campaignId = null, int? depotId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách disbursement công khai cho donor xem (bao gồm items).</summary>
    Task<PagedResult<CampaignDisbursementModel>> GetPublicByCampaignAsync(
        int campaignId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Tính tổng số tiền đã giải ngân từ campaign.</summary>
    Task<decimal> GetTotalDisbursedByCampaignAsync(int campaignId, CancellationToken cancellationToken = default);

    /// <summary>Tạo mới disbursement trong DbContext hiện tại và cho phép đọc ID thật sau khi transaction flush.</summary>
    Task<TrackedFinanceEntityReference<CampaignDisbursementModel>> CreateAsync(CampaignDisbursementModel model, CancellationToken cancellationToken = default);

    /// <summary>Thêm danh sách vật phẩm đã mua vào disbursement (cho donor xem).</summary>
    Task AddItemsAsync(int disbursementId, List<DisbursementItemModel> items, CancellationToken cancellationToken = default);
}
