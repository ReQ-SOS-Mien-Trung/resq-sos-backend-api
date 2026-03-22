using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamDetail;

public class GetRescueTeamDetailQueryHandler(
    IPersonnelQueryRepository personnelQueryRepository,
    IUserRepository userRepository) 
    : IRequestHandler<GetRescueTeamDetailQuery, RescueTeamDetailDto>
{
    public async Task<RescueTeamDetailDto> Handle(GetRescueTeamDetailQuery request, CancellationToken cancellationToken)
    {
        var model = await personnelQueryRepository.GetRescueTeamDetailAsync(request.Id, cancellationToken);
        if (model == null)
            throw new NotFoundException($"Không tìm thấy đội với ID = {request.Id}");

        var manager = await userRepository.GetByIdAsync(model.ManagedBy, cancellationToken);
        var managerName = manager != null ? $"{manager.LastName} {manager.FirstName}".Trim() : string.Empty;

        return new RescueTeamDetailDto
        {
            Id = model.Id,
            Code = model.Code,
            Name = model.Name,
            TeamType = model.TeamType.ToString(),
            Status = model.Status.ToString(),
            AssemblyPointId = model.AssemblyPointId,
            AssemblyPointName = model.AssemblyPointName,
            ManagedBy = managerName,
            MaxMembers = model.MaxMembers,
            CreatedAt = model.CreatedAt.ToVietnamTime(),
            Members = model.Members.Select(m => new RescueTeamMemberDto
            {
                UserId = m.UserId,
                FirstName = m.Profile?.FirstName,
                LastName = m.Profile?.LastName,
                Phone = m.Profile?.Phone,
                AvatarUrl = m.Profile?.AvatarUrl,
                RescuerType = m.Profile?.RescuerType,
                Status = m.Status.ToString(),
                IsLeader = m.IsLeader,
                RoleInTeam = m.RoleInTeam,
                JoinedAt = m.JoinedAt.ToVietnamTime()
            }).ToList()
        };
    }
}
