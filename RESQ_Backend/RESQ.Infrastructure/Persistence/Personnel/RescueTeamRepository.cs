using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class RescueTeamRepository(IUnitOfWork unitOfWork) : IRescueTeamRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    public async Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<RescueTeam>()
            .Include(x => x.AssemblyPoint)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return null;

        var members = await _unitOfWork.Set<RescueTeamMember>().Where(m => m.TeamId == id).ToListAsync(cancellationToken);
        return RescueTeamMapper.ToDomain(entity, members);
    }

    public async Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<RescueTeam>().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (entity == null) return null;

        var members = await _unitOfWork.Set<RescueTeamMember>()
            .Include(m => m.User).ThenInclude(u => u!.RescuerProfile)
            .Where(m => m.TeamId == entity.Id).ToListAsync(cancellationToken);
        return RescueTeamMapper.ToDomain(entity, members);
    }

    public async Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<RescueTeam>().OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        
        var teamIds = items.Select(x => x.Id).ToList();
        var allMembers = await _unitOfWork.Set<RescueTeamMember>()
            .Include(m => m.User).ThenInclude(u => u!.RescuerProfile)
            .Where(m => teamIds.Contains(m.TeamId)).ToListAsync(cancellationToken);

        var domainItems = items.Select(e => RescueTeamMapper.ToDomain(e, allMembers.Where(m => m.TeamId == e.Id).ToList())).ToList();

        return new PagedResult<RescueTeamModel>(domainItems, total, pageNumber, pageSize);
    }

    public async Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<RescueTeamMember>()
            .Include(m => m.Team)
            .AnyAsync(m => m.UserId == userId 
                           && m.Status == TeamMemberStatus.Accepted.ToString()
                           && m.Team!.Status != RescueTeamStatus.Disbanded.ToString(), 
                      cancellationToken);
    }

    public async Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<RescueTeamMember>()
            .Include(m => m.Team)
            .AnyAsync(m => m.UserId == userId
                           && m.IsLeader
                           && m.Status == TeamMemberStatus.Accepted.ToString()
                           && m.Team!.Status != RescueTeamStatus.Disbanded.ToString(),
                      cancellationToken);
    }

    public async Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid memberUserId, CancellationToken cancellationToken = default)
    {
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();

        // Tìm đội mà thành viên đang tham gia
        var teamId = await _unitOfWork.Set<RescueTeamMember>()
            .Include(m => m.Team)
            .Where(m => m.UserId == memberUserId
                        && m.Status == acceptedStatus
                        && m.Team!.Status != disbandedStatus)
            .Select(m => (int?)m.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (teamId == null) return null;

        // Lấy đội trưởng của đội đó
        return await _unitOfWork.Set<RescueTeamMember>()
            .Where(m => m.TeamId == teamId && m.IsLeader && m.Status == acceptedStatus)
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid memberUserId, CancellationToken cancellationToken = default)
    {
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();

        var member = await _unitOfWork.SetTracked<RescueTeamMember>()
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.UserId == memberUserId
                                      && m.Status == acceptedStatus
                                      && m.Team!.Status != disbandedStatus,
                                 cancellationToken);

        if (member == null) return false;

        member.Status = TeamMemberStatus.Removed.ToString();
        return true;
    }

    public async Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<UserAbility>()
            .Include(ua => ua.Ability)
            .ThenInclude(a => a.AbilitySubgroup)
            .ThenInclude(sg => sg.AbilityCategory)
            .AnyAsync(ua => ua.UserId == userId && ua.Ability.AbilitySubgroup!.AbilityCategory!.Code == categoryCode, cancellationToken);
    }

    public async Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var topAbility = await _unitOfWork.Set<UserAbility>()
            .Include(ua => ua.Ability)
            .ThenInclude(a => a.AbilitySubgroup)
            .ThenInclude(sg => sg!.AbilityCategory)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.Level)
            .FirstOrDefaultAsync(cancellationToken);

        return topAbility?.Ability?.AbilitySubgroup?.AbilityCategory?.Code;
    }

    public async Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
    {
        var entity = RescueTeamMapper.ToEntity(team);

        // Persist members via EF navigation property so TeamId FK is auto-resolved on save
        foreach (var member in team.Members)
        {
            entity.RescueTeamMembers.Add(new RescueTeamMember
            {
                UserId = member.UserId,
                Status = member.Status.ToString(),
                InvitedAt = member.JoinedAt,
                IsLeader = member.IsLeader,
                RoleInTeam = member.RoleInTeam,
                CheckedIn = true
            });
        }

        await _unitOfWork.GetRepository<RescueTeam>().AddAsync(entity);
    }

    public async Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<RescueTeam>().FirstOrDefaultAsync(x => x.Id == team.Id, cancellationToken);
        if (entity != null)
        {
            RescueTeamMapper.ToEntity(team, entity);

            var existingMembers = await _unitOfWork.SetTracked<RescueTeamMember>().Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
            
            foreach (var domainMem in team.Members)
            {
                var efMem = existingMembers.FirstOrDefault(m => m.UserId == domainMem.UserId);
                if (efMem == null)
                {
                    await _unitOfWork.GetRepository<RescueTeamMember>().AddAsync(new RescueTeamMember
                    {
                        TeamId = team.Id, 
                        UserId = domainMem.UserId,
                        Status = domainMem.Status.ToString(),
                        InvitedAt = domainMem.JoinedAt, // DB column "invited_at" maps to domain JoinedAt
                        IsLeader = domainMem.IsLeader,
                        RoleInTeam = domainMem.RoleInTeam,
                        CheckedIn = true
                    });
                }
                else
                {
                    efMem.Status = domainMem.Status.ToString();
                }
            }
        }
    }

    public async Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(
        string? abilityKeyword,
        bool? available,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var availableStatuses = new[] { "Available" };

        var query = _unitOfWork.Set<RescueTeam>()
            .Include(t => t.AssemblyPoint)
            .Where(t => t.Status != disbandedStatus);

        if (!string.IsNullOrWhiteSpace(abilityKeyword))
            query = query.Where(t => EF.Functions.ILike(t.TeamType ?? string.Empty, "%" + abilityKeyword + "%"));

        if (available == true)
            query = query.Where(t => availableStatuses.Contains(t.Status ?? string.Empty));

        var total = await query.CountAsync(ct);

        var teams = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.TeamType,
                t.Status,
                t.AssemblyPointId,
                AssemblyPointName = t.AssemblyPoint != null ? t.AssemblyPoint.Name : null,
                AssemblyPointLocation = t.AssemblyPoint != null ? t.AssemblyPoint.Location : null
            })
            .ToListAsync(ct);

        var teamIds = teams.Select(t => t.Id).ToList();
        var memberCounts = await _unitOfWork.Set<RescueTeamMember>()
            .Where(m => teamIds.Contains(m.TeamId) && m.Status == "Accepted")
            .GroupBy(m => m.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count, ct);

        var result = teams.Select(t => new AgentTeamInfo
        {
            TeamId             = t.Id,
            TeamName           = t.Name ?? string.Empty,
            TeamType           = t.TeamType,
            Status             = t.Status ?? string.Empty,
            IsAvailable        = availableStatuses.Contains(t.Status ?? string.Empty),
            MemberCount        = memberCounts.TryGetValue(t.Id, out var mc) ? mc : 0,
            AssemblyPointId    = t.AssemblyPointId,
            AssemblyPointName  = t.AssemblyPointName,
            Latitude           = t.AssemblyPointLocation?.Y,
            Longitude          = t.AssemblyPointLocation?.X
        }).ToList();

        return (result, total);
    }

    public async Task<int> CountActiveTeamsByAssemblyPointAsync(
        int assemblyPointId,
        IEnumerable<int> excludeTeamIds,
        CancellationToken cancellationToken = default)
    {
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var excludeList = excludeTeamIds.ToList();

        return await _unitOfWork.Set<RescueTeam>()
            .Where(t => t.AssemblyPointId == assemblyPointId
                        && t.Status != disbandedStatus
                        && !excludeList.Contains(t.Id))
            .CountAsync(cancellationToken);
    }
}
