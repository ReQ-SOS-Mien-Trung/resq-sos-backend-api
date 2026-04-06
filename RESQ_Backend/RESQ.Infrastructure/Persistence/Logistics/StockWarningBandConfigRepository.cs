using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class StockWarningBandConfigRepository(IUnitOfWork unitOfWork) : IStockWarningBandConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<WarningBandConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<StockWarningBandConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : ToDto(entity);
    }

    public async Task<WarningBandConfigDto> UpsertAsync(
        List<WarningBandDto> bands,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<StockWarningBandConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .FirstOrDefaultAsync(cancellationToken);

        var json = JsonSerializer.Serialize(bands, JsonOpts);
        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new StockWarningBandConfig
            {
                Id = 1,
                BandsJson = json,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };
            await repo.AddAsync(entity);
        }
        else
        {
            entity.BandsJson = json;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();
        return ToDto(entity);
    }

    private static WarningBandConfigDto ToDto(StockWarningBandConfig entity)
    {
        var bands = JsonSerializer.Deserialize<List<WarningBandDto>>(entity.BandsJson, JsonOpts)
                    ?? [];
        return new WarningBandConfigDto
        {
            Id = entity.Id,
            Bands = bands,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
