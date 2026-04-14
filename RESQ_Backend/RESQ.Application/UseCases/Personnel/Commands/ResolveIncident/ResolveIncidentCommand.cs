using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record ResolveIncidentCommand(int TeamId, bool HasInjuredMember) : IRequest;
