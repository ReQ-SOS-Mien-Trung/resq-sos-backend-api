using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public record CreateSosRequestCommand(
    Guid UserId,
    GeoLocation Location,
    string RawMessage,
    Guid? PacketId = null,
    double? LocationAccuracy = null,
    string? SosType = null,
    string? StructuredData = null,
    string? NetworkMetadata = null,
    long? Timestamp = null
) : IRequest<CreateSosRequestResponse>;