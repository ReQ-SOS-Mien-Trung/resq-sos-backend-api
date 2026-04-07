using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Common;

public static class SosRequestVictimUpdateOverlay
{
    public static SosRequestModel Apply(SosRequestModel sosRequest, SosRequestVictimUpdateModel? victimUpdate)
    {
        if (victimUpdate is null || victimUpdate.SosRequestId != sosRequest.Id)
        {
            return sosRequest;
        }

        return new SosRequestModel
        {
            Id = sosRequest.Id,
            PacketId = victimUpdate.PacketId,
            ClusterId = sosRequest.ClusterId,
            UserId = sosRequest.UserId,
            Location = victimUpdate.Location ?? sosRequest.Location,
            LocationAccuracy = victimUpdate.LocationAccuracy,
            SosType = victimUpdate.SosType,
            RawMessage = string.IsNullOrWhiteSpace(victimUpdate.RawMessage)
                ? sosRequest.RawMessage
                : victimUpdate.RawMessage,
            StructuredData = victimUpdate.StructuredData,
            NetworkMetadata = victimUpdate.NetworkMetadata,
            SenderInfo = victimUpdate.SenderInfo,
            VictimInfo = victimUpdate.VictimInfo,
            ReporterInfo = victimUpdate.ReporterInfo,
            IsSentOnBehalf = victimUpdate.IsSentOnBehalf,
            OriginId = victimUpdate.OriginId,
            PriorityLevel = sosRequest.PriorityLevel,
            PriorityScore = sosRequest.PriorityScore,
            Status = sosRequest.Status,
            ReceivedAt = sosRequest.ReceivedAt,
            Timestamp = victimUpdate.Timestamp,
            CreatedAt = victimUpdate.ClientCreatedAt ?? sosRequest.CreatedAt,
            LastUpdatedAt = GetLastUpdatedAt(sosRequest.LastUpdatedAt, victimUpdate.UpdatedAt),
            ReviewedAt = sosRequest.ReviewedAt,
            ReviewedById = sosRequest.ReviewedById,
            CreatedByCoordinatorId = sosRequest.CreatedByCoordinatorId
        };
    }

    private static DateTime? GetLastUpdatedAt(DateTime? originalLastUpdatedAt, DateTime updatedAt)
    {
        if (!originalLastUpdatedAt.HasValue || updatedAt > originalLastUpdatedAt.Value)
        {
            return updatedAt;
        }

        return originalLastUpdatedAt;
    }
}