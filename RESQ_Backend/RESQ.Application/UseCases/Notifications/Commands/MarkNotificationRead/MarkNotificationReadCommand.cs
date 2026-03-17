using MediatR;

namespace RESQ.Application.UseCases.Notifications.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(int UserNotificationId, Guid UserId) : IRequest<bool>;
