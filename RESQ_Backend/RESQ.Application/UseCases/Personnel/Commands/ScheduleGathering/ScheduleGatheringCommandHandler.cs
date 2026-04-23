using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IUnitOfWork unitOfWork,
    IFirebaseService firebaseService,
    ILogger<ScheduleGatheringCommandHandler> logger)
    : IRequestHandler<ScheduleGatheringCommand, int>
{
    public async Task<int> Handle(ScheduleGatheringCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra điểm tập kết tồn tại.
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        if (ap.Status != Domain.Enum.Personnel.AssemblyPointStatus.Available)
        {
            throw new BadRequestException(
                $"Không thể tạo sự kiện tập trung vì điểm tập kết {ap.Name} đang ở trạng thái {ap.Status}. Chỉ cho phép khi điểm tập kết đang Available.");
        }

        // 2. Chuẩn hóa sang UTC để lưu trữ.
        var assemblyDateUtc = request.AssemblyDate.ToUtcForStorage();
        var checkInDeadlineUtc = request.CheckInDeadline.ToUtcForStorage();

        // 3. Không cho phép lập lịch vào một thời điểm đã trôi qua.
        var nowUtc = DateTime.UtcNow;
        if (assemblyDateUtc < nowUtc)
        {
            var nowInVietnam = nowUtc.ToVietnamTime();
            throw new BadRequestException(
                $"Thời gian triệu tập không được là thời điểm trong quá khứ. Thời điểm hiện tại theo giờ Việt Nam là {nowInVietnam:HH:mm dd/MM/yyyy}.");
        }

        // 4. Tạo AssemblyEvent.
        var eventId = await assemblyEventRepository.CreateEventAsync(
            request.AssemblyPointId, assemblyDateUtc, checkInDeadlineUtc, request.CreatedBy, cancellationToken);

        // 5. Snapshot rescuer chưa có team vào sự kiện triệu tập để xếp nhóm.
        var rescuerIds = await assemblyPointRepository.GetTeamlessRescuerUserIdsAsync(
            request.AssemblyPointId, cancellationToken);

        if (rescuerIds.Count > 0)
        {
            await assemblyEventRepository.AssignParticipantsAsync(eventId, rescuerIds, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        // 6. Gửi thông báo Firebase cho tất cả rescuer được gán vào sự kiện.
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var vnAssemblyDate = TimeZoneInfo.ConvertTimeFromUtc(assemblyDateUtc, vnTimeZone);
        var vnCheckInDeadline = TimeZoneInfo.ConvertTimeFromUtc(checkInDeadlineUtc, vnTimeZone);

        var title = "Thông báo triệu tập";
        var body = $"Bạn được triệu tập tập trung tại điểm tập kết \"{ap.Name}\" vào lúc " +
                   $"{vnAssemblyDate:HH:mm} ngày {vnAssemblyDate:dd/MM/yyyy}. " +
                   $"Thời hạn check-in: {vnCheckInDeadline:HH:mm} ngày {vnCheckInDeadline:dd/MM/yyyy}. " +
                   "Vui lòng có mặt đúng giờ và thực hiện check-in trên ứng dụng khi đến nơi.";

        logger.LogInformation(
            "Gửi thông báo triệu tập cho {Count} rescuer tại AP {ApId}, EventId={EventId}",
            rescuerIds.Count,
            request.AssemblyPointId,
            eventId);

        foreach (var userId in rescuerIds)
        {
            try
            {
                await firebaseService.SendNotificationToUserAsync(
                    userId, title, body, "assembly_gathering", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể gửi thông báo cho rescuer {UserId}", userId);
            }
        }

        return eventId;
    }
}
