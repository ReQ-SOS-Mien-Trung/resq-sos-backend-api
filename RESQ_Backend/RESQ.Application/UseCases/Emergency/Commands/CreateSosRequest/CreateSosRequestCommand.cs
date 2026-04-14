using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public record CreateSosRequestCommand(
    Guid UserId,
    GeoLocation Location,
    string RawMessage,
    Guid? PacketId = null,
    string? OriginId = null,
    double? LocationAccuracy = null,
    string? SosType = null,
    string? StructuredData = null,
    string? NetworkMetadata = null,
    string? SenderInfo = null,
    long? Timestamp = null,
    Guid? CreatedByCoordinatorId = null,
    DateTime? ClientCreatedAt = null,
    string? VictimInfo = null,
    bool IsSentOnBehalf = false,
    string? ReporterInfo = null
) : IRequest<CreateSosRequestResponse>;
