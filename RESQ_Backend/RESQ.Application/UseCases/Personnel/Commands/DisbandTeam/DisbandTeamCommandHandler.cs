using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class DisbandTeamCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DisbandTeamCommand>
{
    public async Task Handle(DisbandTeamCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct) 
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        team.Disband();
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
