using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestRepository(ResQDbContext context) : ISosRequestRepository
{
    private readonly ResQDbContext _context = context;

    public async Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
    {
        var entity = SosRequestMapper.ToEntity(sosRequest);
        await _context.SosRequests.AddAsync(entity, cancellationToken);
    }

    public async Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.SosRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(SosRequestMapper.ToDomain);
    }

    public async Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.SosRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(SosRequestMapper.ToDomain);
    }

    public async Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SosRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity == null ? null : SosRequestMapper.ToDomain(entity);
    }
}