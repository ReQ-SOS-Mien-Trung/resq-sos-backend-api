using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class RescueTeamRepository(ResQDbContext context) : IRescueTeamRepository
{
    public async Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.RescueTeams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return null;

        var members = await context.RescueTeamMembers.AsNoTracking().Where(m => m.TeamId == id).ToListAsync(cancellationToken);
        return RescueTeamMapper.ToDomain(entity, members);
    }

    public async Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await context.RescueTeams.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (entity == null) return null;

        var members = await context.RescueTeamMembers.AsNoTracking().Where(m => m.TeamId == entity.Id).ToListAsync(cancellationToken);
        return RescueTeamMapper.ToDomain(entity, members);
    }

    public async Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.RescueTeams.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        
        var teamIds = items.Select(x => x.Id).ToList();
        var allMembers = await context.RescueTeamMembers.AsNoTracking().Where(m => teamIds.Contains(m.TeamId)).ToListAsync(cancellationToken);

        var domainItems = items.Select(e => RescueTeamMapper.ToDomain(e, allMembers.Where(m => m.TeamId == e.Id).ToList())).ToList();

        return new PagedResult<RescueTeamModel>(domainItems, total, pageNumber, pageSize);
    }

    public async Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.RescueTeamMembers
            .Include(m => m.Team)
            .AnyAsync(m => m.UserId == userId 
                           && m.Status == TeamMemberStatus.Accepted.ToString()
                           && m.Team!.Status != RescueTeamStatus.Disbanded.ToString(), 
                      cancellationToken);
    }

    public async Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default)
    {
        return await context.UserAbilities
            .Include(ua => ua.Ability)
            .ThenInclude(a => a.AbilitySubgroup)
            .ThenInclude(sg => sg.AbilityCategory)
            .AnyAsync(ua => ua.UserId == userId && ua.Ability.AbilitySubgroup!.AbilityCategory!.Code == categoryCode, cancellationToken);
    }

    public async Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var topAbility = await context.UserAbilities
            .Include(ua => ua.Ability)
            .ThenInclude(a => a.AbilitySubgroup)
            .ThenInclude(sg => sg.AbilityCategory)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.Level)
            .FirstOrDefaultAsync(cancellationToken);

        return topAbility?.Ability?.AbilitySubgroup?.AbilityCategory?.Code;
    }

    public async Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
    {
        var entity = RescueTeamMapper.ToEntity(team);
        await context.RescueTeams.AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
    {
        var entity = await context.RescueTeams.FirstOrDefaultAsync(x => x.Id == team.Id, cancellationToken);
        if (entity != null)
        {
            RescueTeamMapper.ToEntity(team, entity);

            var existingMembers = await context.RescueTeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
            
            foreach (var domainMem in team.Members)
            {
                var efMem = existingMembers.FirstOrDefault(m => m.UserId == domainMem.UserId);
                if (efMem == null)
                {
                    await context.RescueTeamMembers.AddAsync(new RescueTeamMember
                    {
                        TeamId = team.Id, 
                        UserId = domainMem.UserId,
                        Status = domainMem.Status.ToString(),
                        InvitedAt = domainMem.InvitedAt,
                        RespondedAt = domainMem.RespondedAt,
                        IsLeader = domainMem.IsLeader,
                        RoleInTeam = domainMem.RoleInTeam,
                        CheckedIn = domainMem.CheckedIn
                    }, cancellationToken);
                }
                else
                {
                    efMem.Status = domainMem.Status.ToString();
                    efMem.RespondedAt = domainMem.RespondedAt;
                    efMem.CheckedIn = domainMem.CheckedIn;
                }
            }
        }
    }
}
