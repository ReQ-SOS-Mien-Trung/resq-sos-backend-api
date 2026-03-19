using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
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
        // 1. Validate điểm tập kết tồn tại
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        // 2. Tạo AssemblyEvent (rule: chỉ 1 active event per AP — enforce trong repository)
        var eventId = await assemblyEventRepository.CreateEventAsync(
            request.AssemblyPointId, request.AssemblyDate, request.CreatedBy, cancellationToken);

        // 3. Snapshot: gán tất cả rescuer hiện tại của AP vào sự kiện
        var rescuerIds = await assemblyPointRepository.GetAssignedRescuerUserIdsAsync(
            request.AssemblyPointId, cancellationToken);

        if (rescuerIds.Count > 0)
        {
            await assemblyEventRepository.AssignParticipantsAsync(eventId, rescuerIds, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        // 4. Gửi thông báo Firebase cho tất cả rescuer được gán vào sự kiện
        var title = "📢 RESQ – Thông báo triệu tập";
        var body = $"Bạn được triệu tập tập trung tại điểm tập kết \"{ap.Name}\" vào ngày " +
                   $"{request.AssemblyDate:dd/MM/yyyy HH:mm}.\n" +
                   "Vui lòng có mặt đúng giờ và thực hiện check-in khi đến nơi.";

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
