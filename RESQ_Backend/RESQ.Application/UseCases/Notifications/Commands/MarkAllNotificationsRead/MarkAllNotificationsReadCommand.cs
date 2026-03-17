using MediatR;

namespace RESQ.Application.UseCases.Notifications.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest;
