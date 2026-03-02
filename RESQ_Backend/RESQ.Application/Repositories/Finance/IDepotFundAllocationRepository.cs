using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IDepotFundAllocationRepository
{
    Task<PagedResult<DepotFundAllocationModel>> GetPagedAsync(int pageNumber, int pageSize, int? campaignId = null, int? depotId = null, CancellationToken cancellationToken = default);
    Task CreateAllocationAsync(DepotFundAllocationModel model, CancellationToken cancellationToken = default);
}
