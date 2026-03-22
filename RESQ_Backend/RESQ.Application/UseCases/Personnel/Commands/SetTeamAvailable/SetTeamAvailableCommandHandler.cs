using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamAvailable;

public class SetTeamAvailableCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetTeamAvailableCommand>
{
    public async Task Handle(SetTeamAvailableCommand request, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ id = {request.TeamId}");

        // Domain rule: chỉ leader mới được set Available, và đội phải đang ở Gathering
        team.SetAvailableByLeader(request.LeaderUserId);

        await teamRepository.UpdateAsync(team, cancellationToken);
        await unitOfWork.SaveAsync();
    }
}
