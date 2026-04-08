using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class FundTransactionRepository(IUnitOfWork unitOfWork) : IFundTransactionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<FundTransactionModel>> GetByCampaignIdAsync(
        int campaignId,
        int pageNumber,
        int pageSize,
        List<TransactionType>?          types          = null,
        List<TransactionDirection>?     directions     = null,
        List<TransactionReferenceType>? referenceTypes = null,
        CancellationToken cancellationToken = default)
    {
        // Lowercase cả filter lẫn cột DB để so sánh case-insensitive (PostgreSQL phân biệt hoa/thường)
        var typeStrings          = types?.Select(t => t.ToString().ToLower()).ToList();
        var directionStrings     = directions?.Select(d => d.ToString().ToLower()).ToList();
        var referenceTypeStrings = referenceTypes?.Select(r => r.ToString().ToLower()).ToList();

        var query = _unitOfWork.Set<FundTransaction>()
            .Include(x => x.FundCampaign)
            .Include(x => x.CreatedByUser)
            .Where(x => x.FundCampaignId == campaignId)
            .AsQueryable();

        if (typeStrings != null && typeStrings.Count > 0)
            query = query.Where(x => x.Type != null && typeStrings.Contains(x.Type.ToLower()));

        if (directionStrings != null && directionStrings.Count > 0)
            query = query.Where(x => x.Direction != null && directionStrings.Contains(x.Direction.ToLower()));

        if (referenceTypeStrings != null && referenceTypeStrings.Count > 0)
            query = query.Where(x => x.ReferenceType != null && referenceTypeStrings.Contains(x.ReferenceType.ToLower()));

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(FundTransactionMapper.ToModel).ToList();
        return new PagedResult<FundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task CreateAsync(FundTransactionModel model, CancellationToken cancellationToken = default)
    {
        var entity = FundTransactionMapper.ToEntity(model);
        var repo = _unitOfWork.GetRepository<FundTransaction>();
        await repo.AddAsync(entity);
    }
}
