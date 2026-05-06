using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
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
                    ae.Status == "Gathering")
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

    public async Task<List<AssemblyPointUnavailableAlternativeDto>> GetAvailableAlternativesByDistanceAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var source = await _unitOfWork.Set<AssemblyPoint>()
            .Where(x => x.Id == assemblyPointId)
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return [];
        }

        var availableStatus = RESQ.Domain.Enum.Personnel.AssemblyPointStatus.Available.ToString();
        var alternativeEntities = await _unitOfWork.Set<AssemblyPoint>()
            .Where(x => x.Id != assemblyPointId && x.Status == availableStatus)
            .ToListAsync(cancellationToken);

        var sourceLatitude = source.Location?.Y;
        var sourceLongitude = source.Location?.X;

        var alternatives = alternativeEntities.Select(x => new AssemblyPointUnavailableAlternativeDto
        {
            Id = x.Id,
            Code = x.Code ?? string.Empty,
            Name = x.Name ?? string.Empty,
            MaxCapacity = x.MaxCapacity ?? 0,
            Status = x.Status ?? string.Empty,
            Latitude = x.Location?.Y,
            Longitude = x.Location?.X
        }).ToList();

        foreach (var alternative in alternatives)
        {
            var distanceMeters = CalculateDistanceMeters(
                sourceLatitude,
                sourceLongitude,
                alternative.Latitude,
                alternative.Longitude);
            alternative.DistanceKm = distanceMeters.HasValue
                ? Math.Round(distanceMeters.Value / 1000.0, 1)
                : null;
        }

        return alternatives
            .OrderBy(x => x.DistanceKm ?? double.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<List<AssemblyPointUnavailableTeamlessRescuerDto>> GetTeamlessCheckedInRescuersAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();
        var disbandedStatus = RESQ.Domain.Enum.Personnel.RescueTeamStatus.Disbanded.ToString();

        var checkedInRows = await _unitOfWork.Set<AssemblyParticipant>()
            .Where(p => p.IsCheckedIn && !p.IsCheckedOut)
            .Join(
                _unitOfWork.Set<AssemblyEvent>().Where(e => e.AssemblyPointId == assemblyPointId),
                p => p.AssemblyEventId,
                e => e.Id,
                (p, e) => new { Participant = p, Event = e })
            .Join(
                _unitOfWork.Set<User>().Where(u => u.RoleId == 3).Include(u => u.RescuerProfile),
                pe => pe.Participant.RescuerId,
                u => u.Id,
                (pe, u) => new { pe.Participant, pe.Event, User = u })
            .ToListAsync(cancellationToken);

        if (checkedInRows.Count == 0)
        {
            return [];
        }

        var userIds = checkedInRows.Select(x => x.User.Id).Distinct().ToList();

        var usersInTeam = await _unitOfWork.Set<RescueTeamMember>()
            .Where(m => userIds.Contains(m.UserId)
                && m.Status == acceptedStatus
                && m.Team != null
                && m.Team.Status != disbandedStatus)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var teamlessRows = checkedInRows
            .Where(x => !usersInTeam.Contains(x.User.Id))
            .GroupBy(x => x.User.Id)
            .Select(g => g
                .OrderByDescending(x => x.Participant.CheckInTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Event.AssemblyDate)
                .First())
            .ToList();

        if (teamlessRows.Count == 0)
        {
            return [];
        }

        var teamlessUserIds = teamlessRows.Select(x => x.User.Id).ToList();
        var allAbilities = await _unitOfWork.Set<UserAbility>()
            .Where(ua => teamlessUserIds.Contains(ua.UserId))
            .Include(ua => ua.Ability)
                .ThenInclude(a => a.AbilitySubgroup)
            .ToListAsync(cancellationToken);

        var abilitiesDict = allAbilities
            .GroupBy(ua => ua.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(ua => ua.Level)
                    .Select(ua => ua.Ability?.AbilitySubgroup?.Description ?? ua.Ability?.Description ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .Take(3)
                    .ToList());

        return teamlessRows
            .OrderByDescending(x => x.Participant.CheckInTime ?? DateTime.MinValue)
            .Select(x => new AssemblyPointUnavailableTeamlessRescuerDto
            {
                UserId = x.User.Id,
                FirstName = x.User.FirstName,
                LastName = x.User.LastName,
                Phone = x.User.Phone,
                Email = x.User.Email,
                AvatarUrl = x.User.AvatarUrl,
                RescuerType = x.User.RescuerProfile?.RescuerType,
                CheckedInAt = x.Participant.CheckInTime,
                AssemblyEventId = x.Event.Id,
                TopAbilities = abilitiesDict.TryGetValue(x.User.Id, out var abilities) ? abilities : []
            })
            .ToList();
    }

    public async Task<List<AssemblyPointUnavailableTeamlessRescuerDto>> GetCheckedInRescuersAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var checkedInRows = await _unitOfWork.Set<AssemblyParticipant>()
            .Where(p => p.IsCheckedIn && !p.IsCheckedOut)
            .Join(
                _unitOfWork.Set<AssemblyEvent>().Where(e => e.AssemblyPointId == assemblyPointId),
                p => p.AssemblyEventId,
                e => e.Id,
                (p, e) => new { Participant = p, Event = e })
            .Join(
                _unitOfWork.Set<User>().Where(u => u.RoleId == 3).Include(u => u.RescuerProfile),
                pe => pe.Participant.RescuerId,
                u => u.Id,
                (pe, u) => new { pe.Participant, pe.Event, User = u })
            .ToListAsync(cancellationToken);

        var latestRows = checkedInRows
            .GroupBy(x => x.User.Id)
            .Select(g => g
                .OrderByDescending(x => x.Participant.CheckInTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Event.AssemblyDate)
                .First())
            .ToList();

        if (latestRows.Count == 0)
        {
            return [];
        }

        var userIds = latestRows.Select(x => x.User.Id).ToList();
        var allAbilities = await _unitOfWork.Set<UserAbility>()
            .Where(ua => userIds.Contains(ua.UserId))
            .Include(ua => ua.Ability)
                .ThenInclude(a => a.AbilitySubgroup)
            .ToListAsync(cancellationToken);

        var abilitiesDict = allAbilities
            .GroupBy(ua => ua.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(ua => ua.Level)
                    .Select(ua => ua.Ability?.AbilitySubgroup?.Description ?? ua.Ability?.Description ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .Take(3)
                    .ToList());

        return latestRows
            .OrderByDescending(x => x.Participant.CheckInTime ?? DateTime.MinValue)
            .Select(x => new AssemblyPointUnavailableTeamlessRescuerDto
            {
                UserId = x.User.Id,
                FirstName = x.User.FirstName,
                LastName = x.User.LastName,
                Phone = x.User.Phone,
                Email = x.User.Email,
                AvatarUrl = x.User.AvatarUrl,
                RescuerType = x.User.RescuerProfile?.RescuerType,
                CheckedInAt = x.Participant.CheckInTime,
                AssemblyEventId = x.Event.Id,
                TopAbilities = abilitiesDict.TryGetValue(x.User.Id, out var abilities) ? abilities : []
            })
            .ToList();
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

    public async Task BulkUpdateRescuerAssemblyPointMapAsync(
        IReadOnlyDictionary<Guid, int> assignments,
        CancellationToken cancellationToken = default)
    {
        if (assignments.Count == 0)
        {
            return;
        }

        var userIds = assignments.Keys.ToList();
        var users = await _unitOfWork.SetTracked<User>()
            .Where(u => userIds.Contains(u.Id) && u.RoleId == 3)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var user in users)
        {
            user.AssemblyPointId = assignments[user.Id];
            user.UpdatedAt = now;
        }
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

    private static double? CalculateDistanceMeters(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
        {
            return null;
        }

        const double earthRadiusMeters = 6371000;
        var dLat = DegreesToRadians(lat2.Value - lat1.Value);
        var dLon = DegreesToRadians(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1.Value))
            * Math.Cos(DegreesToRadians(lat2.Value))
            * Math.Sin(dLon / 2)
            * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
