using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;

public record UpdateMissionActivityCommand(
    int ActivityId,
    int? Step,
    string? ActivityCode,
    string? ActivityType,
    string? Description,
    string? Target,
    string? Items,
    double? TargetLatitude,
    double? TargetLongitude
) : IRequest<UpdateMissionActivityResponse>;
