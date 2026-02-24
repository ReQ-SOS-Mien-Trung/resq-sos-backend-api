using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosRequestRepository sosRequestRepository,
    IRescueMissionSuggestionService suggestionService,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    public async Task<GenerateRescueMissionSuggestionResponse> Handle(
        GenerateRescueMissionSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating rescue mission suggestion for {count} SOS requests, RequestedBy={userId}",
            request.SosRequestIds.Count, request.RequestedByUserId);

        // 1. Fetch all requested SOS requests
        var sosRequestSummaries = new List<SosRequestSummary>();
        var notFoundIds = new List<int>();

        foreach (var sosId in request.SosRequestIds.Distinct())
        {
            var sosRequest = await _sosRequestRepository.GetByIdAsync(sosId, cancellationToken);
            if (sosRequest is null)
            {
                notFoundIds.Add(sosId);
                continue;
            }

            sosRequestSummaries.Add(new SosRequestSummary
            {
                Id = sosRequest.Id,
                SosType = sosRequest.SosType,
                RawMessage = sosRequest.RawMessage,
                StructuredData = sosRequest.StructuredData,
                PriorityLevel = sosRequest.PriorityLevel?.ToString(),
                Status = sosRequest.Status.ToString(),
                Latitude = sosRequest.Location?.Latitude,
                Longitude = sosRequest.Location?.Longitude,
                WaitTimeMinutes = sosRequest.WaitTimeMinutes,
                CreatedAt = sosRequest.CreatedAt
            });
        }

        if (notFoundIds.Count > 0)
        {
            _logger.LogWarning("SOS request IDs not found: {ids}", string.Join(", ", notFoundIds));
        }

        if (sosRequestSummaries.Count == 0)
        {
            throw new NotFoundException("Không tìm thấy SOS request nào trong danh sách đã chọn");
        }

        // 2. Call AI to generate suggestion
        var result = await _suggestionService.GenerateSuggestionAsync(sosRequestSummaries, cancellationToken);

        _logger.LogInformation(
            "Rescue mission suggestion result: IsSuccess={isSuccess}, Title={title}, ResponseTime={time}ms",
            result.IsSuccess, result.SuggestedMissionTitle, result.ResponseTimeMs);

        // 3. Map result to response
        return new GenerateRescueMissionSuggestionResponse
        {
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
