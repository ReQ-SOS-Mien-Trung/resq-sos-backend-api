using System.Runtime.CompilerServices;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;

public class StreamRescueMissionSuggestionQueryHandler(
    IMissionContextService missionContextService,
    IRescueMissionSuggestionService suggestionService,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    ISosClusterRepository sosClusterRepository,
    IUnitOfWork unitOfWork,
    ILogger<StreamRescueMissionSuggestionQueryHandler> logger
) : IStreamRequestHandler<StreamRescueMissionSuggestionQuery, SseMissionEvent>
{
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<StreamRescueMissionSuggestionQueryHandler> _logger = logger;

    private const double LowConfidenceThreshold = 0.65;

    public async IAsyncEnumerable<SseMissionEvent> Handle(
        StreamRescueMissionSuggestionQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 1. Load cluster + SOS requests + nearby depots
        yield return new SseMissionEvent { EventType = "status", Data = "Đang tải dữ liệu cluster..." };

        MissionContext? context = null;
        string? contextError = null;
        try
        {
            context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);
        }
        catch (Exception ex)
        {
            contextError = ex.Message;
        }

        if (contextError is not null)
        {
            yield return new SseMissionEvent { EventType = "error", Data = contextError };
            yield break;
        }

        yield return new SseMissionEvent
        {
            EventType = "status",
            Data = $"Đã tải {context!.SosRequests.Count} SOS request, {context.NearbyDepots.Count} kho tiếp tế, {context.NearbyTeams.Count} đội nearby."
        };

        // 2. Stream AI generation — forward every event and capture the final result
        RescueMissionSuggestionResult? aiResult = null;

        await foreach (var evt in _suggestionService.GenerateSuggestionStreamAsync(
            context.SosRequests, context.NearbyDepots, context.NearbyTeams, context.MultiDepotRecommended, cancellationToken))
        {
            if (evt.EventType == "result" && evt.Result is not null)
            {
                aiResult = evt.Result;

                RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(aiResult, context.NearbyTeams);

                if (aiResult.IsSuccess && aiResult.ConfidenceScore < LowConfidenceThreshold)
                {
                    aiResult.NeedsManualReview = true;
                    aiResult.LowConfidenceWarning =
                        $"AI chỉ đạt độ tự tin {aiResult.ConfidenceScore:P0} (ngưỡng: {LowConfidenceThreshold:P0}). " +
                        "Kế hoạch có thể chưa chính xác — điều phối viên nên xem xét và điều chỉnh thủ công.";
                    _logger.LogWarning(
                        "AI low-confidence result for ClusterId={clusterId}: ConfidenceScore={score}",
                        request.ClusterId, aiResult.ConfidenceScore);
                }
                aiResult.MultiDepotRecommended = context.MultiDepotRecommended;
            }

            yield return evt;
        }

        if (aiResult is null) yield break;

        // 3. Persist to DB
        int? savedId = null;
        try
        {
            savedId = await PersistAsync(request.ClusterId, context!.Cluster, aiResult, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist streaming mission suggestion for ClusterId={clusterId}", request.ClusterId);
        }

        if (savedId.HasValue)
            yield return new SseMissionEvent { EventType = "status", Data = $"Đã lưu đề xuất vào hệ thống (ID: {savedId})." };

        yield return new SseMissionEvent { EventType = "status", Data = "done" };
    }

    private async Task<int> PersistAsync(
        int clusterId,
        SosClusterModel cluster,
        RescueMissionSuggestionResult result,
        CancellationToken cancellationToken)
    {
        var activitiesJson = result.SuggestedActivities.Count > 0
            ? JsonSerializer.Serialize(result.SuggestedActivities)
            : null;

        var metadataJson = JsonSerializer.Serialize(new
        {
            result.OverallAssessment,
            result.EstimatedDuration,
            result.SpecialNotes,
            result.SuggestedResources,
            result.SuggestedSeverityLevel,
            result.SuggestedMissionType,
            result.RawAiResponse
        });

        var missionModel = new MissionAiSuggestionModel
        {
            ClusterId              = clusterId,
            ModelName              = result.ModelName,
            AnalysisType           = "RescueMissionSuggestion",
            SuggestedMissionTitle  = result.SuggestedMissionTitle,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            ConfidenceScore        = result.ConfidenceScore,
            Metadata               = metadataJson,
            CreatedAt              = DateTime.UtcNow,
            Activities = activitiesJson is not null
                ? [
                    new ActivityAiSuggestionModel
                    {
                        ClusterId           = clusterId,
                        ModelName           = result.ModelName,
                        ActivityType        = result.SuggestedMissionType ?? "RescueActivities",
                        SuggestionPhase     = "Execution",
                        SuggestedActivities = activitiesJson,
                        ConfidenceScore     = result.ConfidenceScore,
                        CreatedAt           = DateTime.UtcNow
                    }
                  ]
                : []
        };

        var suggestionId = await _missionAiSuggestionRepository.CreateAsync(missionModel, cancellationToken);
        _logger.LogInformation("Saved streaming mission suggestion to DB: SuggestionId={id}", suggestionId);

        cluster.IsMissionCreated = true;
        await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
        await _unitOfWork.SaveAsync();

        return suggestionId;
    }
}
