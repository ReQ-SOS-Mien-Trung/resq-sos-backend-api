namespace RESQ.Application.Services;

/// <summary>
/// Background task item for AI analysis
/// </summary>
public record SosAiAnalysisTask(int SosRequestId, string? StructuredData, string? RawMessage, string? SosType);

/// <summary>
/// Queue for background AI analysis tasks
/// </summary>
public interface ISosAiAnalysisQueue
{
    /// <summary>
    /// Queue a task for background AI analysis
    /// </summary>
    ValueTask QueueAsync(SosAiAnalysisTask task);
}
