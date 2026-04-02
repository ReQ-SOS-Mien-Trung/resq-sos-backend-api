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
    // RoleId = 2 → Coordinator (global hoặc point)
    private const int CoordinatorRoleId = 2;

    public async Task Handle(DisbandTeamCommand request, CancellationToken ct)
    {
        // Kiểm tra role từ JWT claim (không query DB)
        if (request.CallerRoleId != CoordinatorRoleId)
            throw new ForbiddenException("Chỉ coordinator mới có thể giải tán đội cứu hộ.");

        var team = await teamRepository.GetByIdAsync(request.TeamId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        // Domain enforces: chỉ đội Unavailable mới được giải tán
        team.Disband();
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
