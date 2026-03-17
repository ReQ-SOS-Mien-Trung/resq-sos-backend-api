using MediatR;

namespace RESQ.Application.UseCases.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<GetMyNotificationsResponse>;
