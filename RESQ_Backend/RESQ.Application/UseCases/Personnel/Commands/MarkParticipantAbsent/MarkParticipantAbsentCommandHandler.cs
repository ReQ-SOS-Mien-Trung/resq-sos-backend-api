using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.MarkParticipantAbsent;

/// <summary>
/// Xử lý luồng đội trưởng đánh dấu thành viên vắng mặt tại sự kiện tập trung:
/// 1. Kiểm tra sự kiện đang ở trạng thái Gathering.
/// 2. Kiểm tra caller là đội trưởng của một đội đang hoạt động.
/// 3. Đánh dấu participant là Absent (và checkout nếu đang checked-in).
/// 4. Gửi thông báo tới coordinator (người tạo sự kiện) để bổ sung thành viên.
/// </summary>
public class MarkParticipantAbsentCommandHandler(
    IAssemblyEventRepository assemblyEventRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<MarkParticipantAbsentCommand>
{
    public async Task Handle(MarkParticipantAbsentCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate sự kiện tồn tại và đang Gathering
        var evt = await assemblyEventRepository.GetEventByIdAsync(request.EventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.EventId}");

        if (evt.Status != AssemblyEventStatus.Gathering.ToString())
            throw new BadRequestException(
                $"Chỉ có thể đánh dấu vắng mặt khi sự kiện đang ở trạng thái Gathering. Trạng thái hiện tại: {evt.Status}");

        // 2. Validate caller không tự đánh dấu bản thân
        if (request.CallerUserId == request.TargetRescuerId)
            throw new BadRequestException("Đội trưởng không thể tự đánh dấu vắng mặt cho chính mình.");

        // 3. Validate caller là đội trưởng của một đội đang hoạt động
        var isLeader = await rescueTeamRepository.IsLeaderInActiveTeamAsync(request.CallerUserId, cancellationToken);
        if (!isLeader)
            throw new ForbiddenException("Chỉ đội trưởng của đội đang hoạt động mới có thể đánh dấu vắng mặt thành viên.");

        // 4. Đánh dấu participant vắng mặt
        var success = await assemblyEventRepository.MarkParticipantAbsentAsync(
            request.EventId, request.TargetRescuerId, cancellationToken);

        if (!success)
            throw new NotFoundException(
                $"Không tìm thấy thành viên (id = {request.TargetRescuerId}) trong sự kiện tập trung id = {request.EventId}");

        // 5. Lưu thay đổi
        await unitOfWork.SaveAsync();

        // 6. Gửi thông báo cho coordinator
        var coordinatorId = await assemblyEventRepository.GetEventCreatedByAsync(request.EventId, cancellationToken);
        if (coordinatorId.HasValue)
        {
            // Lấy tên thành viên vắng để đưa vào nội dung thông báo
            var absentUser = await userRepository.GetByIdAsync(request.TargetRescuerId, cancellationToken);
            var absentName = absentUser != null
                ? $"{absentUser.FirstName} {absentUser.LastName}".Trim()
                : request.TargetRescuerId.ToString();

            await firebaseService.SendNotificationToUserAsync(
                coordinatorId.Value,
                "Thành viên vắng mặt",
                $"{absentName} đã bị đánh dấu vắng mặt tại sự kiện tập trung. Vui lòng bổ sung thành viên khác.",
                "absent_participant",
                new Dictionary<string, string>
                {
                    { "eventId", request.EventId.ToString() },
                    { "absentRescuerId", request.TargetRescuerId.ToString() }
                },
                cancellationToken);
        }
    }
}
