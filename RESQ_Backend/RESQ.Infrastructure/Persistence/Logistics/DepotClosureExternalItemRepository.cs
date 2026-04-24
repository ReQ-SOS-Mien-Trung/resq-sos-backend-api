using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotClosureExternalItemRepository(IUnitOfWork unitOfWork) : IDepotClosureExternalItemRepository
{
    public async Task CreateBulkAsync(IEnumerable<CreateClosureExternalItemDto> items, CancellationToken cancellationToken = default)
    {
        var repo = unitOfWork.GetRepository<DepotClosureExternalItem>();
        foreach (var dto in items)
        {
            await repo.AddAsync(new DepotClosureExternalItem
            {
                DepotId = dto.DepotId,
                ClosureId = dto.ClosureId,
                ItemModelId = dto.ItemModelId,
                LotId = dto.LotId,
                ReusableItemId = dto.ReusableItemId,
                ItemName = dto.ItemName,
                CategoryName = dto.CategoryName,
                ItemType = dto.ItemType,
                Unit = dto.Unit,
                SerialNumber = dto.SerialNumber,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.TotalPrice,
                HandlingMethod = dto.HandlingMethod,
                Recipient = dto.Recipient,
                Note = dto.Note,
                ImageUrl = dto.ImageUrl,
                ProcessedBy = dto.ProcessedBy,
                ProcessedAt = dto.ProcessedAt,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public async Task<List<DepotClosureExternalItemDetailDto>> GetByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entities = await unitOfWork.Set<DepotClosureExternalItem>()
            .AsNoTracking()
            .Where(x => x.ClosureId == closureId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(x =>
        {
            var parsed = ExternalDispositionMetadata.Parse(x.HandlingMethod);
            return new DepotClosureExternalItemDetailDto
            {
                Id = x.Id,
                ItemModelId = x.ItemModelId,
                LotId = x.LotId,
                ReusableItemId = x.ReusableItemId,
                ItemName = x.ItemName,
                CategoryName = x.CategoryName,
                ItemType = x.ItemType,
                Unit = x.Unit,
                SerialNumber = x.SerialNumber,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TotalPrice = x.TotalPrice,
                HandlingMethod = parsed?.ToString() ?? x.HandlingMethod,
                HandlingMethodDisplay = parsed.HasValue
                    ? ExternalDispositionMetadata.GetDisplayValue(parsed.Value)
                    : x.HandlingMethod,
                Recipient = x.Recipient,
                Note = x.Note,
                ImageUrl = x.ImageUrl,
                ProcessedBy = x.ProcessedBy,
                ProcessedAt = x.ProcessedAt,
                CreatedAt = x.CreatedAt
            };
        }).ToList();
    }
}
