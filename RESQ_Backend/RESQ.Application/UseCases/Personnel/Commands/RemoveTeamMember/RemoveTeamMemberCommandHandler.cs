using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class RemoveTeamMemberCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveTeamMemberCommand>
{
    public async Task Handle(RemoveTeamMemberCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        var targetMember = team.Members.FirstOrDefault(m => m.UserId == request.UserId && m.Status != TeamMemberStatus.Removed)
            ?? throw new NotFoundException("Không tìm thấy thành viên trong đội.");

        if (targetMember.IsLeader)
            throw new BadRequestException("Không được xóa đội trưởng khỏi đội.");

        var isCallerLeaderOfThisTeam = team.Members.Any(m =>
            m.UserId == request.CallerUserId &&
            m.IsLeader &&
            m.Status == TeamMemberStatus.Accepted);

        if (!request.CanOverrideTeamMemberRemoval && !isCallerLeaderOfThisTeam)
            throw new ForbiddenException("Chỉ đội trưởng của chính đội hoặc coordinator mới có thể xóa thành viên.");

        team.RemoveMember(request.UserId);
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
