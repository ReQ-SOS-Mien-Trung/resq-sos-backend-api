using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;

public class GetMissionSuggestionsQueryHandler(
    IMissionAiSuggestionRepository missionRepository,
    ISosClusterRepository sosClusterRepository,
    ILogger<GetMissionSuggestionsQueryHandler> logger
) : IRequestHandler<GetMissionSuggestionsQuery, GetMissionSuggestionsResponse>
{
    private readonly IMissionAiSuggestionRepository _missionRepository = missionRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ILogger<GetMissionSuggestionsQueryHandler> _logger = logger;

    public async Task<GetMissionSuggestionsResponse> Handle(
        GetMissionSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting mission suggestions for ClusterId={clusterId}", request.ClusterId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        var suggestions = (await _missionRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken)).ToList();

        var missionDtos = suggestions.Select(m => new MissionSuggestionDto
        {
            Id = m.Id,
            ClusterId = m.ClusterId,
            ModelName = m.ModelName,
            AnalysisType = m.AnalysisType,
            SuggestedMissionTitle = m.SuggestedMissionTitle,
            SuggestedPriorityScore = m.SuggestedPriorityScore,
            ConfidenceScore = m.ConfidenceScore,
            SuggestionScope = m.SuggestionScope,
            CreatedAt = m.CreatedAt,
            Activities = m.Activities.Select(a => new ActivitySuggestionDto
            {
                Id = a.Id,
                ActivityType = a.ActivityType,
                SuggestionPhase = a.SuggestionPhase,
                ConfidenceScore = a.ConfidenceScore,
                CreatedAt = a.CreatedAt,
                SuggestedActivities = DeserializeActivities(a.SuggestedActivities)
            }).ToList()
        }).ToList();

        return new GetMissionSuggestionsResponse
        {
            ClusterId = request.ClusterId,
            TotalSuggestions = missionDtos.Count,
            MissionSuggestions = missionDtos
        };
    }

    private static List<SuggestedActivityDto> DeserializeActivities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<SuggestedActivityDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
