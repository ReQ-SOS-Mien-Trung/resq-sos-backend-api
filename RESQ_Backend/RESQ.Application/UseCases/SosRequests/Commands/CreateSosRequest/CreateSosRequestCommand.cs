using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.SosRequests.Commands.CreateSosRequest;

public record CreateSosRequestCommand(
    Guid UserId,
    GeoLocation Location,
    string RawMessage
) : IRequest<CreateSosRequestResponse>;