using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Notifications;

namespace RESQ.Application.UseCases.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandHandler(INotificationRepository notificationRepository)
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;

    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var success = await _notificationRepository.MarkAsReadAsync(
            request.UserNotificationId, request.UserId, cancellationToken);

        if (!success)
            throw new NotFoundException($"Không tìm thấy notification với ID {request.UserNotificationId}.");

        return true;
    }
}
