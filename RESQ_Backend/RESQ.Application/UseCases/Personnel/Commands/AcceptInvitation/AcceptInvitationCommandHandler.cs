using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.AcceptInvitation;

public class AcceptInvitationCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AcceptInvitationCommand>
{
    public async Task Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct) 
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");
        
        if (await teamRepository.IsUserInActiveTeamAsync(request.UserId, ct))
            throw new ConflictException("Bạn đã chấp nhận tham gia một đội cứu hộ khác.");

        team.AcceptInvitation(request.UserId);
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
