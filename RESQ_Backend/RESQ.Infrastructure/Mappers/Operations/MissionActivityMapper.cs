using System.Text.Json;
using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionActivityMapper
{
    private static readonly Dictionary<MissionActivityStatus, string> StatusToString = new()
    {
        [MissionActivityStatus.Planned] = nameof(MissionActivityStatus.Planned),
        [MissionActivityStatus.OnGoing] = nameof(MissionActivityStatus.OnGoing),
        [MissionActivityStatus.Succeed] = nameof(MissionActivityStatus.Succeed),
        [MissionActivityStatus.PendingConfirmation] = nameof(MissionActivityStatus.PendingConfirmation),
        [MissionActivityStatus.Failed] = nameof(MissionActivityStatus.Failed),
        [MissionActivityStatus.Cancelled] = nameof(MissionActivityStatus.Cancelled)
    };

    private static readonly Dictionary<string, MissionActivityStatus> StringToStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        [NormalizeStatusKey(nameof(MissionActivityStatus.Planned))] = MissionActivityStatus.Planned,
        [NormalizeStatusKey("planned")] = MissionActivityStatus.Planned,
        [NormalizeStatusKey(nameof(MissionActivityStatus.OnGoing))] = MissionActivityStatus.OnGoing,
        [NormalizeStatusKey("on_going")] = MissionActivityStatus.OnGoing,
        [NormalizeStatusKey(nameof(MissionActivityStatus.Succeed))] = MissionActivityStatus.Succeed,
        [NormalizeStatusKey("succeed")] = MissionActivityStatus.Succeed,
        [NormalizeStatusKey(nameof(MissionActivityStatus.PendingConfirmation))] = MissionActivityStatus.PendingConfirmation,
        [NormalizeStatusKey("pending_confirmation")] = MissionActivityStatus.PendingConfirmation,
        [NormalizeStatusKey(nameof(MissionActivityStatus.Failed))] = MissionActivityStatus.Failed,
        [NormalizeStatusKey("failed")] = MissionActivityStatus.Failed,
        [NormalizeStatusKey(nameof(MissionActivityStatus.Cancelled))] = MissionActivityStatus.Cancelled,
        [NormalizeStatusKey("cancelled")] = MissionActivityStatus.Cancelled
    };

    public static string ToDbString(MissionActivityStatus status) =>
        StatusToString.GetValueOrDefault(status, nameof(MissionActivityStatus.Planned));

    public static MissionActivityStatus ToEnum(string? status) =>
        status is not null && StringToStatus.TryGetValue(NormalizeStatusKey(status.Trim()), out var val)
            ? val
            : MissionActivityStatus.Planned;

    private static string NormalizeStatusKey(string status) =>
        status.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    public static MissionActivity ToEntity(MissionActivityModel model)
    {
        var entity = new MissionActivity
        {
            MissionId = model.MissionId,
            Step = model.Step,
            ActivityCode = model.ActivityCode,
            ActivityType = model.ActivityType,
            Description = model.Description,
            Target = EnsureValidJson(model.Target),
            Items = EnsureValidJson(model.Items),
            Status = ToDbString(model.Status),
            AssignedAt = model.AssignedAt,
            CompletedAt = model.CompletedAt,
            LastDecisionBy = model.LastDecisionBy,
            CompletedBy = model.CompletedBy,
            MissionTeamId = model.MissionTeamId,
            Priority = model.Priority,
            EstimatedTime = model.EstimatedTime,
            SosRequestId = model.SosRequestId,
            DepotId = model.DepotId,
            DepotName = model.DepotName,
            DepotAddress = model.DepotAddress,
            AssemblyPointId = model.AssemblyPointId
        };

        if (model.TargetLatitude.HasValue && model.TargetLongitude.HasValue)
        {
            entity.TargetLocation = new Point(model.TargetLongitude.Value, model.TargetLatitude.Value) { SRID = 4326 };
        }

        return entity;
    }

    public static MissionActivityModel ToDomain(MissionActivity entity)
    {
        return new MissionActivityModel
        {
            Id = entity.Id,
            MissionId = entity.MissionId,
            Step = entity.Step,
            ActivityCode = entity.ActivityCode,
            ActivityType = entity.ActivityType,
            Description = entity.Description,
            Target = entity.Target,
            Items = entity.Items,
            TargetLatitude = entity.TargetLocation?.Y,
            TargetLongitude = entity.TargetLocation?.X,
            Status = ToEnum(entity.Status),
            AssignedAt = entity.AssignedAt,
            CompletedAt = entity.CompletedAt,
            LastDecisionBy = entity.LastDecisionBy,
            CompletedBy = entity.CompletedBy,
            MissionTeamId = entity.MissionTeamId,
            Priority = entity.Priority,
            EstimatedTime = entity.EstimatedTime,
            SosRequestId = entity.SosRequestId,
            DepotId = entity.DepotId,
            DepotName = entity.DepotName,
            DepotAddress = entity.DepotAddress,
            AssemblyPointId = entity.AssemblyPointId,
            AssemblyPointName = entity.AssemblyPoint?.Name,
            AssemblyPointLatitude = entity.AssemblyPoint?.Location?.Y,
            AssemblyPointLongitude = entity.AssemblyPoint?.Location?.X
        };
    }

    /// <summary>
    /// Ensures the value stored in a jsonb column is valid JSON.
    /// If the string is already valid JSON it is returned as-is.
    /// Otherwise it is serialized as a JSON string literal so Postgres accepts it.
    /// </summary>
    internal static string? EnsureValidJson(string? value)
    {
        if (value is null) return null;
        try
        {
            using var _ = JsonDocument.Parse(value);
            return value; // already valid JSON
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value); // wrap plain string as JSON string literal
        }
    }
}
