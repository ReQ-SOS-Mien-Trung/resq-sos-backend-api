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
        if (!request.CanDisbandTeam)
            throw new ForbiddenException("Caller hiện tại không có quyền giải tán đội cứu hộ.");

        var team = await teamRepository.GetByIdAsync(request.TeamId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        // Domain enforces: chỉ đội Unavailable mới được giải tán
        team.Disband();
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
