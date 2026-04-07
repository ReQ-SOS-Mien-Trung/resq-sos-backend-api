using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Emergency.Commands.UpdateSosRequestVictim;

public record UpdateSosRequestVictimCommand(
    int SosRequestId,
    Guid RequestedByUserId,
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
    DateTime? ClientCreatedAt = null,
    string? VictimInfo = null,
    bool? IsSentOnBehalf = null,
    string? ReporterInfo = null
) : IRequest<UpdateSosRequestVictimResponse>;