using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamUnavailable;

public class SetTeamUnavailableCommandHandler(
    IRescueTeamRepository teamRepository,
    IAdminRealtimeHubService adminRealtimeHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetTeamUnavailableCommand>
{
    public async Task Handle(SetTeamUnavailableCommand request, CancellationToken cancellationToken)
    {
        // 1. Load team kèm thành viên để kiểm tra leadership
        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ id = {request.TeamId}");

        // 2. Kiểm tra quyền: caller có thể override bằng permission,
        //    còn lại rescuer chỉ được nếu là đội trưởng đang Accepted trong đội này.
        bool isTeamLeader  = team.Members.Any(m =>
            m.UserId == request.CallerUserId
            && m.IsLeader
            && m.Status == TeamMemberStatus.Accepted);

        if (!request.CanOverrideTeamAvailability && !isTeamLeader)
            throw new ForbiddenException("Chỉ đội trưởng hoặc caller có quyền override mới có thể đánh dấu đội không sẵn sàng.");

        if (team.Status == RescueTeamStatus.OnMission || team.Status == RescueTeamStatus.Assigned || team.Status == RescueTeamStatus.Stuck)
        {
            throw new BadRequestException("Đội cứu hộ không được chuyển sang trạng thái Unavailable khi đang trong nhiệm vụ.");
        }

        // 3. Thực hiện chuyển trạng thái (domain logic giữ nguyên)
        team.SetUnavailable();

        await teamRepository.UpdateAsync(team, cancellationToken);
        await unitOfWork.SaveAsync();
        await adminRealtimeHubService.PushRescueTeamUpdateAsync(
            new AdminRescueTeamRealtimeUpdate
            {
                EntityId = team.Id,
                EntityType = "RescueTeam",
                TeamId = team.Id,
                Action = "SetUnavailable",
                Status = team.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}
