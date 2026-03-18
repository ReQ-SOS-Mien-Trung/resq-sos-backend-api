using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundingRequestRepository
{
    Task<FundingRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    Task<PagedResult<FundingRequestModel>> GetPagedAsync(
        int pageNumber, int pageSize,
        int? depotId = null, string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo mới funding request, lưu ngay và trả về ID được sinh ra từ DB.</summary>
    Task<int> CreateAsync(FundingRequestModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(FundingRequestModel model, CancellationToken cancellationToken = default);
}
