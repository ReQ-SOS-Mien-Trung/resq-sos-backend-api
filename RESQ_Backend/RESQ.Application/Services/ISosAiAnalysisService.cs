using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

/// <summary>
/// Service for AI-based SOS request analysis
/// </summary>
public interface ISosAiAnalysisService
{
    /// <summary>
    /// Analyze SOS request using AI and save results to database
    /// </summary>
    Task AnalyzeAndSaveAsync(int sosRequestId, string? structuredData, string? rawMessage, string? sosType, CancellationToken cancellationToken = default);
}
