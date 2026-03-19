using MediatR;

namespace RESQ.Application.UseCases.Notifications.Commands.BroadcastAlert;

public record BroadcastAlertLocationData(string? City, double? Lat, double? Lon);

public record BroadcastAlertItemData(
    string? Id,
    string? EventType,
    string? Title,
    string? Severity,
    List<string>? AreasAffected,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Description,
    List<string>? InstructionChecklist,
    string? Source
);

public record BroadcastAlertCommand(
    Guid SentByUserId,
    BroadcastAlertLocationData? Location,
    List<BroadcastAlertItemData>? ActiveAlerts
) : IRequest<BroadcastAlertResponse>;

public record BroadcastAlertResponse(int SentCount);
