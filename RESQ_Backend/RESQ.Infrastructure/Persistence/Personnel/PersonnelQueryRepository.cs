using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class PersonnelQueryRepository(IUnitOfWork unitOfWork) : IPersonnelQueryRepository
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

        var members = await unitOfWork.GetRepository<RescueTeamMember>().GetAllByPropertyAsync(
            filter: m => m.UserId == userId
                         && m.Status == accepted
                         && m.Team != null
                         && m.Team.Status != disbanded,
            includeProperties: "Team,Team.AssemblyPoint,Team.RescueTeamMembers,Team.RescueTeamMembers.User"
        );

        var team = members
            .Select(m => m.Team)
            .Where(t => t is not null)
            .OrderByDescending(t => t!.CreatedAt)
            .FirstOrDefault();
        if (team is null) return null;

        return RescueTeamMapper.ToDomain(team, team.RescueTeamMembers.ToList());
    }
}
