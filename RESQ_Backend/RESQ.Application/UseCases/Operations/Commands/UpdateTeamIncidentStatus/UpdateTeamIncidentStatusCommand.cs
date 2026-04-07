using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;

public record UpdateTeamIncidentStatusCommand(
    int IncidentId,
    TeamIncidentStatus NewStatus,
    /// <summary>Optional when moving from Reported: true → InProgress, false → Closed</summary>
    bool? NeedsAssistance,
    /// <summary>Used when resolving with/without injured members</summary>
    bool? HasInjuredMember,
    Guid UpdatedBy
) : IRequest<UpdateTeamIncidentStatusResponse>;
