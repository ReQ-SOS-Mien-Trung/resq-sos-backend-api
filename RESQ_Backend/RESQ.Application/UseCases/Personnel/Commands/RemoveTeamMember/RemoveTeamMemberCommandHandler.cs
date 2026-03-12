using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class RemoveTeamMemberCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveTeamMemberCommand>
{
    public async Task Handle(RemoveTeamMemberCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct) 
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        team.RemoveMember(request.UserId);
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
