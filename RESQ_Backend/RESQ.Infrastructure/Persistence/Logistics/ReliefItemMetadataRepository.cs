using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class ReliefItemMetadataRepository(IUnitOfWork unitOfWork, ResQDbContext context) : IReliefItemMetadataRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResQDbContext _context = context;

    public async Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.GetRepository<ReliefItem>()
            .GetAllByPropertyAsync(r => true);

        return items
            .OrderBy(r => r.Id)
            .Select(r => new MetadataDto
            {
                Key   = r.Id.ToString(),
                Value = r.Name ?? string.Empty
            })
            .ToList();
    }

    public async Task<List<MetadataDto>> GetByCategoryCodeAsync(
        ItemCategoryCode categoryCode,
        CancellationToken cancellationToken = default)
    {
        var categoryCodeString = categoryCode.ToString();

        var items = await (
            from ri in _context.ReliefItems.AsNoTracking()
            join cat in _context.ItemCategories.AsNoTracking() on ri.CategoryId equals cat.Id
            where cat.Code == categoryCodeString
            orderby ri.Id
            select new MetadataDto
            {
                Key = ri.Id.ToString(),
                Value = ri.Name ?? string.Empty
            }
        ).ToListAsync(cancellationToken);

        return items;
    }
}
