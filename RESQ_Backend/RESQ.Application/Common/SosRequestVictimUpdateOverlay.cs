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
            PacketId = victimUpdate.PacketId ?? sosRequest.PacketId,
            ClusterId = sosRequest.ClusterId,
            UserId = sosRequest.UserId,
            Location = victimUpdate.Location ?? sosRequest.Location,
            LocationAccuracy = victimUpdate.LocationAccuracy ?? sosRequest.LocationAccuracy,
            SosType = string.IsNullOrWhiteSpace(victimUpdate.SosType)
                ? sosRequest.SosType
                : victimUpdate.SosType,
            RawMessage = string.IsNullOrWhiteSpace(victimUpdate.RawMessage)
                ? sosRequest.RawMessage
                : victimUpdate.RawMessage,
            StructuredData = string.IsNullOrWhiteSpace(victimUpdate.StructuredData)
                ? sosRequest.StructuredData
                : victimUpdate.StructuredData,
            NetworkMetadata = string.IsNullOrWhiteSpace(victimUpdate.NetworkMetadata)
                ? sosRequest.NetworkMetadata
                : victimUpdate.NetworkMetadata,
            SenderInfo = string.IsNullOrWhiteSpace(victimUpdate.SenderInfo)
                ? sosRequest.SenderInfo
                : victimUpdate.SenderInfo,
            VictimInfo = string.IsNullOrWhiteSpace(victimUpdate.VictimInfo)
                ? sosRequest.VictimInfo
                : victimUpdate.VictimInfo,
            ReporterInfo = string.IsNullOrWhiteSpace(victimUpdate.ReporterInfo)
                ? sosRequest.ReporterInfo
                : victimUpdate.ReporterInfo,
            IsSentOnBehalf = victimUpdate.IsSentOnBehalf,
            OriginId = string.IsNullOrWhiteSpace(victimUpdate.OriginId)
                ? sosRequest.OriginId
                : victimUpdate.OriginId,
            PriorityLevel = sosRequest.PriorityLevel,
            PriorityScore = sosRequest.PriorityScore,
            Status = sosRequest.Status,
            ReceivedAt = sosRequest.ReceivedAt,
            Timestamp = victimUpdate.Timestamp ?? sosRequest.Timestamp,
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
