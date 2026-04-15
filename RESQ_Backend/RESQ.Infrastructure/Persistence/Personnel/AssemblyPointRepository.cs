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

    public async Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default, string? statusFilter = null)
    {
        var apQuery     = _unitOfWork.Set<AssemblyPoint>();
        var eventsQuery = _unitOfWork.Set<AssemblyEvent>();

        var filtered = statusFilter != null
            ? apQuery.Where(x => x.Status == statusFilter)
            : apQuery;

        var totalCount = await filtered.CountAsync(cancellationToken);

        // Single round-trip: EXISTS subquery for HasActiveEvent is folded into the projection
        var projected = await filtered
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

        var teams = await _unitOfWork.Set<RescueTeam>()
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

    // -- Rescuer assigned to AP --------------------------------------

    public async Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
    {
        // 1. Rescuer được gán trực tiếp vào AP qua User.AssemblyPointId
        var directIds = _unitOfWork.Set<User>()
            .Where(u => u.AssemblyPointId == assemblyPointId && u.RoleId == 3)
            .Select(u => u.Id);

        // 2. Rescuer thuộc rescue team đang hoạt động tại AP (qua RescueTeamMember)
        var teamMemberIds = _unitOfWork.Set<RescueTeamMember>()
            .Where(m => m.Team != null && m.Team.AssemblyPointId == assemblyPointId)
            .Select(m => m.UserId);

        // Gộp 2 nguồn, loại trùng
        return await directIds
            .Union(teamMemberIds)
            .ToListAsync(cancellationToken);
    }

    private static readonly string _disbandedStatus = RESQ.Domain.Enum.Personnel.RescueTeamStatus.Disbanded.ToString();

    public async Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
    {
        // Rescuer được gán trực tiếp vào AP
        var rescuerAtAp = _unitOfWork.Set<User>()
            .Where(u => u.AssemblyPointId == assemblyPointId && u.RoleId == 3)
            .Select(u => u.Id);

        // Rescuer đã thuộc team đang hoạt động (không Disbanded, status Accepted)
        var rescuerWithTeam = _unitOfWork.Set<RescueTeamMember>()
            .Where(m => m.Status == "Accepted"
                     && m.Team != null
                     && m.Team.Status != _disbandedStatus)
            .Select(m => m.UserId);

        // Chỉ lấy rescuer tại AP mà CHƯA có team
        return await rescuerAtAp
            .Where(id => !rescuerWithTeam.Contains(id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<RescueTeamMember>()
            .AnyAsync(m => m.UserId == rescuerUserId
                        && m.Status == "Accepted"
                        && m.Team != null
                        && m.Team.Status != _disbandedStatus,
                cancellationToken);
    }

    public async Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.SetTracked<User>().FirstOrDefaultAsync(u => u.Id == rescuerUserId, cancellationToken);
        if (user != null)
        {
            user.AssemblyPointId = assemblyPointId;
            user.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(
        IReadOnlyList<Guid> userIds,
        int? assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Single round-trip: bulk UPDATE for all matching rescuers in one statement
        await _unitOfWork.GetRepository<User>().AsQueryable(tracked: false)
            .Where(u => userIds.Contains(u.Id) && u.RoleId == 3)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.AssemblyPointId, assemblyPointId)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);

        // Return only the IDs that actually exist and are rescuers (for downstream processing)
        return await _unitOfWork.GetRepository<User>().AsQueryable(tracked: false)
            .Where(u => userIds.Contains(u.Id) && u.RoleId == 3)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var withTeam = _unitOfWork.Set<RescueTeamMember>()
            .Where(m => userIds.Contains(m.UserId)
                     && m.Status == "Accepted"
                     && m.Team != null
                     && m.Team.Status != _disbandedStatus)
            .Select(m => m.UserId);

        return await _unitOfWork.GetRepository<User>().AsQueryable(tracked: false)
            .Where(u => userIds.Contains(u.Id) && !withTeam.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _unitOfWork.GetRepository<User>().AsQueryable(tracked: false)
            .Where(u => u.AssemblyPointId == assemblyPointId && u.RoleId == 3)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.AssemblyPointId, (int?)null)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);
    }
}
