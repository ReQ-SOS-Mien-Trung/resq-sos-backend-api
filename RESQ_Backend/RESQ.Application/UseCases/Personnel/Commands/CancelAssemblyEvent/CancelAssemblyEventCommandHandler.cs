using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CancelAssemblyEvent;

public class CancelAssemblyEventCommandHandler(
    IAssemblyEventRepository assemblyEventRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork,
    IFirebaseService firebaseService,
    ILogger<CancelAssemblyEventCommandHandler> logger)
    : IRequestHandler<CancelAssemblyEventCommand, CancelAssemblyEventResponse>
{
    public async Task<CancelAssemblyEventResponse> Handle(CancelAssemblyEventCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("CancelAssemblyEvent: EventId={EventId}, CancelledBy={CancelledBy}", request.EventId, request.CancelledBy);

        var assemblyEvent = await assemblyEventRepository.GetEventByIdAsync(request.EventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.EventId}");

        if (!string.Equals(assemblyEvent.Status, AssemblyEventStatus.Gathering.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                $"Chỉ có thể hủy sự kiện khi đang ở trạng thái {AssemblyEventStatus.Gathering}. Trạng thái hiện tại: {assemblyEvent.Status}.");
        }

        var assemblyPoint = await assemblyPointRepository.GetByIdAsync(assemblyEvent.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {assemblyEvent.AssemblyPointId}");

        await assemblyEventRepository.UpdateEventStatusAsync(
            request.EventId,
            AssemblyEventStatus.Cancelled.ToString(),
            cancellationToken);

        var participantIds = await assemblyEventRepository.GetParticipantIdsAsync(request.EventId, cancellationToken);
        foreach (var userId in participantIds)
        {
            try
            {
                await firebaseService.SendNotificationToUserAsync(
                    userId,
                    "Sự kiện tập hợp đã bị hủy",
                    $"Sự kiện tập hợp tại điểm tập kết \"{assemblyPoint.Name}\" đã bị hủy. Vui lòng chờ hướng dẫn điều phối tiếp theo.",
                    "assembly_event_cancelled",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send cancellation notification to user {UserId} for event {EventId}", userId, request.EventId);
            }
        }

        await unitOfWork.SaveAsync();

        return new CancelAssemblyEventResponse
        {
            EventId = request.EventId,
            Status = AssemblyEventStatus.Cancelled.ToString(),
            Message = "Sự kiện tập trung đã được hủy."
        };
    }
}
