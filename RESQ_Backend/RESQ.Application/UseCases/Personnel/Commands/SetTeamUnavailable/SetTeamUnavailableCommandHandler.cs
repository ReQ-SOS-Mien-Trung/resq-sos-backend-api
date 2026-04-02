using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamUnavailable;

public class SetTeamUnavailableCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetTeamUnavailableCommand>
{
    // RoleId = 2 → Coordinator (global hoặc point)
    private const int CoordinatorRoleId = 2;

    public async Task Handle(SetTeamUnavailableCommand request, CancellationToken cancellationToken)
    {
        // 1. Load team kèm thành viên để kiểm tra leadership
        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ id = {request.TeamId}");

        // 2. Kiểm tra quyền: role đọc từ JWT claim (không query DB)
        //    Coordinator (roleId=2) được phép với mọi đội.
        //    Rescuer chỉ được nếu là đội trưởng đang Accepted trong đội này.
        bool isCoordinator = request.CallerRoleId == CoordinatorRoleId;
        bool isTeamLeader  = team.Members.Any(m =>
            m.UserId == request.CallerUserId
            && m.IsLeader
            && m.Status == TeamMemberStatus.Accepted);

        if (!isCoordinator && !isTeamLeader)
            throw new ForbiddenException("Chỉ đội trưởng hoặc coordinator mới có thể đánh dấu đội không sẵn sàng.");

        // 3. Thực hiện chuyển trạng thái (domain logic giữ nguyên)
        team.SetUnavailable();

        await teamRepository.UpdateAsync(team, cancellationToken);
        await unitOfWork.SaveAsync();
    }
}
