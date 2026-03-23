using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Domain.Entities.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class AssemblyPointRepository(IUnitOfWork unitOfWork) : IAssemblyPointRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var entity = AssemblyPointMapper.ToEntity(model);
        await _unitOfWork.GetRepository<AssemblyPoint>().AddAsync(entity);
    }

    public async Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<AssemblyPoint>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == model.Id,
            tracked: true
        );

        if (existingEntity != null)
        {
            AssemblyPointMapper.UpdateEntity(existingEntity, model);
            await repository.UpdateAsync(existingEntity);
        }
    }

    // REVERTED: Standard Physical Delete
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<AssemblyPoint>().DeleteAsyncById(id);
    }

    public async Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Code == code, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var apQuery     = _unitOfWork.GetRepository<AssemblyPoint>().AsQueryable();
        var eventsQuery = _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable();

        var totalCount = await apQuery.CountAsync(cancellationToken);

        // Single round-trip: EXISTS subquery for HasActiveEvent is folded into the projection
        var projected = await apQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(ap => new
            {
                Entity = ap,
                HasActiveEvent = eventsQuery.Any(ae =>
                    ae.AssemblyPointId == ap.Id &&
                    (ae.Status == "Scheduled" || ae.Status == "Gathering"))
            })
            .ToListAsync(cancellationToken);

        var domainItems = projected.Select(x =>
        {
            var model = AssemblyPointMapper.ToDomain(x.Entity);
            model.HasActiveEvent = x.HasActiveEvent;
            return model;
        }).ToList();

        return new PagedResult<AssemblyPointModel>(domainItems, totalCount, pageNumber, pageSize);
    }

    public async Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetAllByPropertyAsync(filter: null);

        return entities.Select(AssemblyPointMapper.ToDomain).ToList();
    }

    public async Task<Dictionary<int, List<AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();

        var teams = await _unitOfWork.GetRepository<RescueTeam>().AsQueryable()
            .Where(t => t.AssemblyPointId.HasValue && idList.Contains(t.AssemblyPointId.Value))
            .Include(t => t.RescueTeamMembers)
                .ThenInclude(m => m.User)
            .ToListAsync(cancellationToken);

        return teams
            .GroupBy(t => t.AssemblyPointId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => new AssemblyPointTeamDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    TeamType = t.TeamType,
                    Status = t.Status,
                    MaxMembers = t.MaxMembers,
                    Members = t.RescueTeamMembers.Select(m => new AssemblyPointTeamMemberDto
                    {
                        UserId = m.UserId,
                        FirstName = m.User?.FirstName,
                        LastName = m.User?.LastName,
                        AvatarUrl = m.User?.AvatarUrl,
                        RoleInTeam = m.RoleInTeam,
                        IsLeader = m.IsLeader,
                        Status = m.Status
                    }).ToList()
                }).ToList()
            );
    }

    // ── Rescuer assigned to AP ──────────────────────────────────────

    public async Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<User>().AsQueryable()
            .Where(u => u.AssemblyPointId == assemblyPointId && u.RoleId == 3)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.GetRepository<User>().AsQueryable(tracked: true).FirstOrDefaultAsync(u => u.Id == rescuerUserId, cancellationToken);
        if (user != null)
        {
            user.AssemblyPointId = assemblyPointId;
            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}
