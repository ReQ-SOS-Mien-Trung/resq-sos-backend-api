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

    /// <summary>Tạo mới disbursement, lưu ngay và trả về ID được sinh ra từ DB.</summary>
    Task<int> CreateAsync(CampaignDisbursementModel model, CancellationToken cancellationToken = default);

    /// <summary>Thêm danh sách vật tư đã mua vào disbursement (cho donor xem).</summary>
    Task AddItemsAsync(int disbursementId, List<DisbursementItemModel> items, CancellationToken cancellationToken = default);
}
