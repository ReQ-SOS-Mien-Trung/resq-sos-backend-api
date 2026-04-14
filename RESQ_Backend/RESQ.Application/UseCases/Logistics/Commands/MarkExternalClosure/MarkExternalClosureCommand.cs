using MediatR;
namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public record MarkExternalClosureCommand(
    int DepotId,
    Guid AdminUserId,
    string? ExternalNote
) : IRequest<MarkExternalClosureResponse>;
