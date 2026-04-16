using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Shared;

public sealed class MissionManualOverrideInfo
{
    public bool IgnoreMixedMissionWarning { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? OverriddenBy { get; set; }
    public DateTime? OverriddenAt { get; set; }
}

internal static class MissionManualOverrideJsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static MissionManualOverrideInfo? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MissionManualOverrideInfo>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    internal static string Serialize(MissionManualOverrideInfo info) =>
        JsonSerializer.Serialize(info, JsonOptions);
}
