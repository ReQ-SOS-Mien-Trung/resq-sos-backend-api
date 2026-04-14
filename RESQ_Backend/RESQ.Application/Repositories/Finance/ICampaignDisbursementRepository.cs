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

    /// <summary>L?y danh sách disbursement công khai cho donor xem (bao g?m items).</summary>
    Task<PagedResult<CampaignDisbursementModel>> GetPublicByCampaignAsync(
        int campaignId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Tính t?ng s? ti?n dã gi?i ngân t? campaign.</summary>
    Task<decimal> GetTotalDisbursedByCampaignAsync(int campaignId, CancellationToken cancellationToken = default);

    /// <summary>T?o m?i disbursement, luu ngay và tr? v? ID du?c sinh ra t? DB.</summary>
    Task<int> CreateAsync(CampaignDisbursementModel model, CancellationToken cancellationToken = default);

    /// <summary>Thêm danh sách v?t ph?m dã mua vào disbursement (cho donor xem).</summary>
    Task AddItemsAsync(int disbursementId, List<DisbursementItemModel> items, CancellationToken cancellationToken = default);
}
