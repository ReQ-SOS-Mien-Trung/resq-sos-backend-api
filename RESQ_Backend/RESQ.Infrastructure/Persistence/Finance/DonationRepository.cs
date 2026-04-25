using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;
using System.Linq.Expressions;

namespace RESQ.Infrastructure.Persistence.Finance;

public class DonationRepository(IUnitOfWork unitOfWork) : IDonationRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<DonationModel>> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        int? campaignId = null, 
        bool? isPrivate = null,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Donation>();
        
        // Ensure successful payments only
        var successStatus = Status.Succeed.ToString();

        // Build composite filter using explicit checks to ensure EF Translation works well
        Expression<Func<Donation, bool>> compositeFilter = x => 
            x.Status == successStatus &&
            (!campaignId.HasValue || x.FundCampaignId == campaignId) &&
            (!isPrivate.HasValue || x.IsPrivate == isPrivate);

        var pagedEntities = await repo.GetPagedAsync(
            pageNumber,
            pageSize,
            compositeFilter,
            q => q.OrderByDescending(x => x.CreatedAt),
            "FundCampaign"
        );

        var models = pagedEntities.Items.Select(DonationMapper.ToModel).ToList();

        return new PagedResult<DonationModel>(models, pagedEntities.TotalCount, pageNumber, pageSize);
    }

    public async Task<DonationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Donation>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "FundCampaign");
            
        return entity == null ? null : DonationMapper.ToModel(entity);
    }

    public async Task<DonationModel?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Donation>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == id, tracked: true, includeProperties: "FundCampaign");

        return entity == null ? null : DonationMapper.ToModel(entity);
    }

    public async Task<DonationModel?> GetByOrderIdAsync(string? orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(orderId)) return null;

        var repo = _unitOfWork.GetRepository<Donation>();
        var entity = await repo.GetByPropertyAsync(x => x.OrderId == orderId, tracked: true, includeProperties: "FundCampaign");

        return entity == null ? null : DonationMapper.ToModel(entity);
    }

    public async Task<List<DonationModel>> GetPendingDonationsPastDeadlineAsync(DateTime currentTimeUtc, CancellationToken cancellationToken = default)
    {
        var pendingStatus = Status.Pending.ToString();

        var entities = await _unitOfWork.Set<Donation>()
            .Where(x => x.Status == pendingStatus &&
                        x.ResponseDeadline.HasValue &&
                        x.ResponseDeadline < currentTimeUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(DonationMapper.ToModel).ToList();
    }

    public async Task CreateAsync(DonationModel model, CancellationToken cancellationToken = default)
    {
        var entity = DonationMapper.ToEntity(model);
        var repo = _unitOfWork.GetRepository<Donation>();
        await repo.AddAsync(entity);
    }

    public async Task UpdateAsync(DonationModel model, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Donation>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == model.Id);
        
        if (entity != null)
        {
            DonationMapper.UpdateEntity(entity, model);
            await repo.UpdateAsync(entity);
        }
    }
}

