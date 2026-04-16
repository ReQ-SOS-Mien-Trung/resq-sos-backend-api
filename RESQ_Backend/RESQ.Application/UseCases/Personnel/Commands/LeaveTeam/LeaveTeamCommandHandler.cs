using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.LeaveTeam;

/// <summary>
/// Rescuer tự rời đội: soft-remove khỏi rescue_team_members,
/// thông báo cho team leader và coordinator (team.ManagedBy).
/// </summary>
public class LeaveTeamCommandHandler(
    IPersonnelQueryRepository personnelQueryRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LeaveTeamCommand>
{
    public async Task Handle(LeaveTeamCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy đội hiện tại của rescuer
        var team = await personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(request.RescuerId, cancellationToken)
            ?? throw new BadRequestException("Bạn không thuộc đội cứu hộ nào để rời.");

        // 2. Chặn rời đội khi team đang trong nhiệm vụ
        if (team.Status is RescueTeamStatus.Assigned or RescueTeamStatus.OnMission or RescueTeamStatus.Stuck)
            throw new BadRequestException(
                $"Không thể rời đội khi đội đang ở trạng thái '{team.Status}'. " +
                "Vui lòng hoàn thành nhiệm vụ trước khi rời đội.");

        // 3. Lấy leader trước khi remove (cần status Accepted còn nguyên trong memory)
        var leader = team.Members.FirstOrDefault(m => m.IsLeader && m.Status == TeamMemberStatus.Accepted);

        // 4. Soft-remove
        var removed = await rescueTeamRepository.SoftRemoveMemberFromActiveTeamAsync(request.RescuerId, cancellationToken);
        if (!removed)
            throw new BadRequestException("Không tìm thấy thông tin thành viên trong đội đang hoạt động.");

        await unitOfWork.SaveAsync();

        // 5. Lấy tên rescuer để đưa vào thông báo
        var rescuer = await userRepository.GetByIdAsync(request.RescuerId, cancellationToken);
        var rescuerName = rescuer != null
            ? $"{rescuer.LastName} {rescuer.FirstName}".Trim()
            : request.RescuerId.ToString();

        var notifyData = new Dictionary<string, string>
        {
            { "teamId", team.Id.ToString() },
            { "rescuerId", request.RescuerId.ToString() }
        };

        // 5. Thông báo cho đội trưởng (nếu không phải chính họ)
        if (leader != null && leader.UserId != request.RescuerId)
        {
            await firebaseService.SendNotificationToUserAsync(
                leader.UserId,
                "Thành viên đã rời đội",
                $"{rescuerName} đã rời khỏi đội \"{team.Name}\".",
                "member_left_team",
                notifyData,
                cancellationToken);
        }

        // 6. Thông báo cho coordinator (ManagedBy) nếu không phải chính họ hoặc leader
        if (team.ManagedBy != request.RescuerId)
        {
            await firebaseService.SendNotificationToUserAsync(
                team.ManagedBy,
                "Thành viên đã rời đội",
                $"{rescuerName} đã rời khỏi đội \"{team.Name}\". Vui lòng bổ sung thành viên nếu cần.",
                "member_left_team",
                notifyData,
                cancellationToken);
        }
    }
}
