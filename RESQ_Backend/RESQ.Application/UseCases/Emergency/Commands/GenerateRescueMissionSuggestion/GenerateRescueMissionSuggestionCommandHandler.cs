using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IRescueMissionSuggestionService suggestionService,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    public async Task<GenerateRescueMissionSuggestionResponse> Handle(
        GenerateRescueMissionSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating rescue mission suggestion for ClusterId={clusterId}, RequestedBy={userId}",
            request.ClusterId, request.RequestedByUserId);

        // 1. Validate cluster exists
        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        // 2. Load all SOS requests belonging to the cluster
        var clusterSosRequests = await _sosRequestRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken);
        var sosRequestList = clusterSosRequests.ToList();

        if (sosRequestList.Count == 0)
            throw new BadRequestException($"Cluster {request.ClusterId} không có SOS request nào");

        var sosRequestSummaries = sosRequestList.Select(sos => new SosRequestSummary
        {
            Id = sos.Id,
            SosType = sos.SosType,
            RawMessage = sos.RawMessage,
            StructuredData = sos.StructuredData,
            PriorityLevel = sos.PriorityLevel?.ToString(),
            Status = sos.Status.ToString(),
            Latitude = sos.Location?.Latitude,
            Longitude = sos.Location?.Longitude,
            WaitTimeMinutes = sos.WaitTimeMinutes,
            CreatedAt = sos.CreatedAt
        }).ToList();

        // 3. Call AI to generate suggestion
        var result = await _suggestionService.GenerateSuggestionAsync(sosRequestSummaries, cancellationToken);

        _logger.LogInformation(
            "Rescue mission suggestion result: IsSuccess={isSuccess}, Title={title}, ResponseTime={time}ms",
            result.IsSuccess, result.SuggestedMissionTitle, result.ResponseTimeMs);

        // 4. Persist to DB (always save, even partial results)
        int? savedSuggestionId = null;
        try
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
                ClusterId = request.ClusterId,
                ModelName = result.ModelName,
                AnalysisType = "RescueMissionSuggestion",
                SuggestedMissionTitle = result.SuggestedMissionTitle,
                SuggestedPriorityScore = result.SuggestedPriorityScore,
                ConfidenceScore = result.ConfidenceScore,
                Metadata = metadataJson,
                CreatedAt = DateTime.UtcNow,
                Activities = activitiesJson is not null
                    ? [
                        new ActivityAiSuggestionModel
                        {
                            ClusterId = request.ClusterId,
                            ModelName = result.ModelName,
                            ActivityType = result.SuggestedMissionType ?? "RescueActivities",
                            SuggestionPhase = "Execution",
                            SuggestedActivities = activitiesJson,
                            ConfidenceScore = result.ConfidenceScore,
                            CreatedAt = DateTime.UtcNow
                        }
                      ]
                    : []
            };

            savedSuggestionId = await _missionAiSuggestionRepository.CreateAsync(missionModel, cancellationToken);
            _logger.LogInformation("Saved mission suggestion to DB: SuggestionId={id}", savedSuggestionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist mission suggestion to DB for ClusterId={clusterId}", request.ClusterId);
        }

        // 5. Map result to response
        return new GenerateRescueMissionSuggestionResponse
        {
            SuggestionId = savedSuggestionId,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            ModelName = result.ModelName,
            ResponseTimeMs = result.ResponseTimeMs,
            SosRequestCount = sosRequestSummaries.Count,
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            OverallAssessment = result.OverallAssessment,
            SuggestedActivities = result.SuggestedActivities,
            SuggestedResources = result.SuggestedResources,
            EstimatedDuration = result.EstimatedDuration,
            SpecialNotes = result.SpecialNotes,
            ConfidenceScore = result.ConfidenceScore
        };
    }
}
