using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamAvailable;

public class SetTeamAvailableCommandHandler(
    IRescueTeamRepository teamRepository,
    IAdminRealtimeHubService adminRealtimeHubService,
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
        await adminRealtimeHubService.PushRescueTeamUpdateAsync(
            new AdminRescueTeamRealtimeUpdate
            {
                EntityId = team.Id,
                EntityType = "RescueTeam",
                TeamId = team.Id,
                Action = "SetAvailable",
                Status = team.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}
