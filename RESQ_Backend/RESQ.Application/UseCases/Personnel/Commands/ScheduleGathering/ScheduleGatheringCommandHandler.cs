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
        // 1. Validate assembly point ton tai.
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        // 2. Normalize ve UTC de luu tru.
        var assemblyDateUtc = request.AssemblyDate.ToUtcForStorage();

        // 3. Khong cho phep lap lich vao ngay qua khu theo gio Viet Nam.
        var assemblyDateInVietnam = assemblyDateUtc.ToVietnamTime().Date;
        var todayInVietnam = DateTime.UtcNow.ToVietnamTime().Date;
        if (assemblyDateInVietnam < todayInVietnam)
        {
            throw new BadRequestException(
                $"Ngày triệu tập không được là ngày quá khứ. Ngày hiện tại theo giờ Việt Nam là {todayInVietnam:dd/MM/yyyy}.");
        }

        // 4. Tao AssemblyEvent (rule: chi 1 active event per AP, enforce trong repository).
        var eventId = await assemblyEventRepository.CreateEventAsync(
            request.AssemblyPointId, assemblyDateUtc, request.CreatedBy, cancellationToken);

        // 5. Snapshot rescuer chua co team vao su kien trieu tap de xep nhom.
        var rescuerIds = await assemblyPointRepository.GetTeamlessRescuerUserIdsAsync(
            request.AssemblyPointId, cancellationToken);

        if (rescuerIds.Count > 0)
        {
            await assemblyEventRepository.AssignParticipantsAsync(eventId, rescuerIds, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        // 6. Gui thong bao Firebase cho tat ca rescuer duoc gan vao su kien.
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var vnAssemblyDate = TimeZoneInfo.ConvertTimeFromUtc(assemblyDateUtc, vnTimeZone);

        var title = "Thông báo triệu tập";
        var body = $"Bạn được triệu tập tập trung tại điểm tập kết \"{ap.Name}\" vào lúc " +
                   $"{vnAssemblyDate:HH:mm} ngày {vnAssemblyDate:dd/MM/yyyy}. " +
                   "Vui lòng có mặt đúng giờ và thực hiện check-in trên ứng dụng khi đến nơi.";

        logger.LogInformation("Gửi thông báo triệu tập cho {Count} rescuer tại AP {ApId}, EventId={EventId}",
            rescuerIds.Count, request.AssemblyPointId, eventId);

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
