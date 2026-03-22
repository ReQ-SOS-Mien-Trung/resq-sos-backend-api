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
    /// <summary>Số giờ tối thiểu từ thời điểm hiện tại đến ngày triệu tập.</summary>
    private const int MinHoursInAdvance = 48;

    public async Task<int> Handle(ScheduleGatheringCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate điểm tập kết tồn tại
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        // 2. Validate ngày triệu tập phải sau ít nhất 48 giờ
        var minAllowedDate = DateTime.UtcNow.AddHours(MinHoursInAdvance);
        if (request.AssemblyDate < minAllowedDate)
            throw new BadRequestException(
                $"Ngày triệu tập phải sau ít nhất {MinHoursInAdvance} giờ kể từ thời điểm hiện tại. " +
                $"Thời gian sớm nhất cho phép: {minAllowedDate:dd/MM/yyyy HH:mm} UTC.");

        // 3. Tạo AssemblyEvent (rule: chỉ 1 active event per AP — enforce trong repository)
        var eventId = await assemblyEventRepository.CreateEventAsync(
            request.AssemblyPointId, request.AssemblyDate, request.CreatedBy, cancellationToken);

        // 4. Snapshot: gán tất cả rescuer hiện tại của AP vào sự kiện
        var rescuerIds = await assemblyPointRepository.GetAssignedRescuerUserIdsAsync(
            request.AssemblyPointId, cancellationToken);

        if (rescuerIds.Count > 0)
        {
            await assemblyEventRepository.AssignParticipantsAsync(eventId, rescuerIds, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        // 5. Gửi thông báo Firebase cho tất cả rescuer được gán vào sự kiện
        // Chuyển đổi UTC sang múi giờ Việt Nam (UTC+7) cho hiển thị
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var vnAssemblyDate = TimeZoneInfo.ConvertTimeFromUtc(request.AssemblyDate, vnTimeZone);

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
