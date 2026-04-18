namespace RESQ.Infrastructure.Options;

public sealed class MissionSuggestionPipelineOptions
{
    /// <summary>
    /// Bật pipeline 4 stage + final validator. Khi tắt, hệ thống dùng legacy MissionPlanning flow.
    /// </summary>
    public bool UseMissionSuggestionPipeline { get; set; } = true;
}
