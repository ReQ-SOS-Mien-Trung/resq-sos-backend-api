using Microsoft.EntityFrameworkCore;
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
                DepotId        = dto.DepotId,
                ClosureId      = dto.ClosureId,
                ItemName       = dto.ItemName,
                CategoryName   = dto.CategoryName,
                ItemType       = dto.ItemType,
                Unit           = dto.Unit,
                Quantity       = dto.Quantity,
                UnitPrice      = dto.UnitPrice,
                TotalPrice     = dto.TotalPrice,
                HandlingMethod = dto.HandlingMethod,
                Recipient      = dto.Recipient,
                Note           = dto.Note,
                ImageUrl       = dto.ImageUrl,
                ProcessedBy    = dto.ProcessedBy,
                ProcessedAt    = dto.ProcessedAt,
                CreatedAt      = DateTime.UtcNow
            });
        }
    }
}
