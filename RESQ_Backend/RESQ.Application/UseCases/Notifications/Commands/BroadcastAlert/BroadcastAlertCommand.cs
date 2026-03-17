using MediatR;

namespace RESQ.Application.UseCases.Notifications.Commands.BroadcastAlert;

public record BroadcastAlertCommand(
    Guid SentByUserId,
    string Title,
    string Body,
    string Type = "flood_alert") : IRequest<BroadcastAlertResponse>;

public record BroadcastAlertResponse(int SentCount);
