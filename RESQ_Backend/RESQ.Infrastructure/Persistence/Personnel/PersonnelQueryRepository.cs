using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class PersonnelQueryRepository(IUnitOfWork unitOfWork, ResQDbContext context) : IPersonnelQueryRepository
{
    public async Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(
        int pageNumber, int pageSize,
        string? firstName = null, string? lastName = null,
        string? phone = null, string? email = null,
        RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null,
        CancellationToken cancellationToken = default)
    {
        var activeTeamStatus = TeamMemberStatus.Accepted.ToString();
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var rescuerTypeStr = rescuerType?.ToString();

        // 1. Get User IDs of users who are currently in active teams
        var teamMembers = await unitOfWork.GetRepository<RescueTeamMember>().GetAllByPropertyAsync(
            filter: m => m.Status == activeTeamStatus && m.Team != null && m.Team.Status != disbandedStatus,
            includeProperties: "Team"
        );
        
        var activeTeamUserIds = teamMembers.Select(m => m.UserId).Distinct().ToList();

        // 2. Fetch Paginated Users who are eligible, have role = 3 (Rescuer), and not in the active teams list
        var pagedUsers = await unitOfWork.GetRepository<User>().GetPagedAsync(
            pageNumber,
            pageSize,
            filter: u => u.RoleId == 3 && u.IsEligibleRescuer && !activeTeamUserIds.Contains(u.Id)
                && (firstName == null || (u.FirstName != null && u.FirstName.Contains(firstName)))
                && (lastName == null || (u.LastName != null && u.LastName.Contains(lastName)))
                && (phone == null || (u.Phone != null && u.Phone.Contains(phone)))
                && (email == null || (u.Email != null && u.Email.Contains(email)))
                && (rescuerTypeStr == null || u.RescuerType == rescuerTypeStr),
            orderBy: q => q.OrderByDescending(u => u.CreatedAt)
        );

        // 3. Map to Domain Models using Mapper
        var userModels = pagedUsers.Items.Select(FreeRescuerMapper.ToModel).ToList();

        // 4. Populate top abilities for the fetched users
        if (userModels.Any())
        {
            var userIds = userModels.Select(u => u.Id).ToList();
            var abilities = await unitOfWork.GetRepository<UserAbility>().GetAllByPropertyAsync(
                filter: ua => userIds.Contains(ua.UserId),
                includeProperties: "Ability"
            );

            var groupedAbilities = abilities.GroupBy(ua => ua.UserId).ToList();

            foreach (var user in userModels)
            {
                var userAbilities = groupedAbilities.FirstOrDefault(a => a.Key == user.Id);
                if (userAbilities != null)
                {
                    user.TopAbilities = userAbilities
                        .OrderByDescending(ua => ua.Level)
                        .Select(ua => ua.Ability.Code)
                        .Take(3)
                        .ToList();
                }
            }
        }

        return new PagedResult<FreeRescuerModel>(userModels, pagedUsers.TotalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        // 1. Fetch Paginated Teams including related data
        var pagedTeams = await unitOfWork.GetRepository<RescueTeam>().GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(t => t.CreatedAt),
            includeProperties: "AssemblyPoint,RescueTeamMembers"
        );

        // 2. Map to Domain Models using Mapper
        var teamModels = pagedTeams.Items.Select(t => RescueTeamMapper.ToDomain(t, t.RescueTeamMembers.ToList())).ToList();

        return new PagedResult<RescueTeamModel>(teamModels, pagedTeams.TotalCount, pageNumber, pageSize);
    }

    public async Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default)
    {
        // 1. Fetch Single Team with detailed members (User info included for Profile Value Object mapping)
        var team = await unitOfWork.GetRepository<RescueTeam>().GetByPropertyAsync(
            filter: t => t.Id == teamId,
            tracked: false,
            includeProperties: "AssemblyPoint,RescueTeamMembers,RescueTeamMembers.User"
        );

        if (team == null) return null;

        // 2. Map to Domain Model with loaded profiles
        return RescueTeamMapper.ToDomain(team, team.RescueTeamMembers.ToList());
    }

    public async Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accepted = TeamMemberStatus.Accepted.ToString();
        var disbanded = RescueTeamStatus.Disbanded.ToString();

        // Query from RescueTeam side to avoid a cycle: RescueTeamMember → Team → RescueTeamMembers (same root type)
        var team = await unitOfWork.GetRepository<RescueTeam>().GetByPropertyAsync(
            filter: t => t.Status != disbanded
                         && t.RescueTeamMembers.Any(m => m.UserId == userId && m.Status == accepted),
            tracked: false,
            includeProperties: "AssemblyPoint,RescueTeamMembers,RescueTeamMembers.User"
        );

        if (team is null) return null;

        return RescueTeamMapper.ToDomain(team, team.RescueTeamMembers.ToList());
    }

    public async Task<PagedResult<FreeRescuerModel>> GetRescuersByAssemblyPointAsync(
        int assemblyPointId,
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();

        // 1. Lấy tất cả đội (chưa Disbanded) thuộc điểm tập kết
        var teams = await unitOfWork.GetRepository<RescueTeam>().GetAllByPropertyAsync(
            filter: t => t.AssemblyPointId == assemblyPointId && t.Status != disbandedStatus
        );

        var teamIds = teams.Select(t => t.Id).ToList();

        if (teamIds.Count == 0)
            return new PagedResult<FreeRescuerModel>([], 0, pageNumber, pageSize);

        // 2. Lấy các member đã Accepted trong những đội đó, kèm thông tin User
        var members = await unitOfWork.GetRepository<RescueTeamMember>().GetAllByPropertyAsync(
            filter: m => teamIds.Contains(m.TeamId) && m.Status == acceptedStatus,
            includeProperties: "User"
        );

        // 3. Deduplicate theo UserId (một user có thể trong nhiều đội)
        var users = members
            .Where(m => m.User != null)
            .Select(m => m.User!)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        var totalCount = users.Count;
        var pagedUsers = users
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var models = pagedUsers.Select(FreeRescuerMapper.ToModel).ToList();

        return new PagedResult<FreeRescuerModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<RescuerModel>> GetRescuersAsync(
        int pageNumber, int pageSize,
        bool? hasAssemblyPoint = null,
        bool? hasTeam = null,
        RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null,
        string? abilitySubgroupCode = null,
        string? abilityCategoryCode = null,
        CancellationToken cancellationToken = default)
    {
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var rescuerTypeStr = rescuerType?.ToString();

        // Subquery: user IDs đang trong đội active
        var inTeamUserIds = context.RescueTeamMembers
            .Where(m => m.Status == acceptedStatus && m.Team!.Status != disbandedStatus)
            .Select(m => m.UserId);

        // Subquery: user IDs đang trong đội active có assembly point
        var inApUserIds = context.RescueTeamMembers
            .Where(m => m.Status == acceptedStatus && m.Team!.Status != disbandedStatus && m.Team.AssemblyPointId != null)
            .Select(m => m.UserId);

        // Base: eligible rescuers (roleId = 3)
        var query = context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == 3 && u.IsEligibleRescuer);

        // Filter: rescuerType
        if (rescuerTypeStr != null)
            query = query.Where(u => u.RescuerType == rescuerTypeStr);

        // Filter: ability subgroup
        if (abilitySubgroupCode != null)
            query = query.Where(u => u.UserAbilities.Any(ua =>
                ua.Ability.AbilitySubgroup != null &&
                ua.Ability.AbilitySubgroup.Code == abilitySubgroupCode));

        // Filter: ability category
        if (abilityCategoryCode != null)
            query = query.Where(u => u.UserAbilities.Any(ua =>
                ua.Ability.AbilitySubgroup != null &&
                ua.Ability.AbilitySubgroup.AbilityCategory != null &&
                ua.Ability.AbilitySubgroup.AbilityCategory.Code == abilityCategoryCode));

        // Filter: hasTeam
        if (hasTeam.HasValue)
        {
            if (hasTeam.Value)
                query = query.Where(u => inTeamUserIds.Contains(u.Id));
            else
                query = query.Where(u => !inTeamUserIds.Contains(u.Id));
        }

        // Filter: hasAssemblyPoint
        if (hasAssemblyPoint.HasValue)
        {
            if (hasAssemblyPoint.Value)
                query = query.Where(u => inApUserIds.Contains(u.Id));
            else
                query = query.Where(u => !inApUserIds.Contains(u.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserAbilities)
                .ThenInclude(ua => ua.Ability)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        // Load team + assembly point info for the fetched users
        var teamMemberData = await context.RescueTeamMembers
            .AsNoTracking()
            .Where(m => userIds.Contains(m.UserId) &&
                        m.Status == acceptedStatus &&
                        m.Team!.Status != disbandedStatus)
            .Include(m => m.Team)
            .ToListAsync(cancellationToken);

        var teamByUser = teamMemberData
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Team).FirstOrDefault());

        var models = users.Select(u =>
        {
            teamByUser.TryGetValue(u.Id, out var activeTeam);
            return new RescuerModel
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Phone = u.Phone,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                RescuerType = u.RescuerType,
                Address = u.Address,
                Ward = u.Ward,
                Province = u.Province,
                HasTeam = activeTeam != null,
                HasAssemblyPoint = activeTeam?.AssemblyPointId != null,
                TopAbilities = u.UserAbilities
                    .OrderByDescending(ua => ua.Level)
                    .Select(ua => ua.Ability.Code)
                    .Take(3)
                    .ToList()
            };
        }).ToList();

        return new PagedResult<RescuerModel>(models, totalCount, pageNumber, pageSize);
    }
}
