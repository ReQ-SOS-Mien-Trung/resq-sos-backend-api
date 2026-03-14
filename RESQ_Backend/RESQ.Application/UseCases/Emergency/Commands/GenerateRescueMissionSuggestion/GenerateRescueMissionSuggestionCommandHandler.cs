using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosClusterRepository sosClusterRepository,
    IMissionContextService missionContextService,
    IRescueMissionSuggestionService suggestionService,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    IUnitOfWork unitOfWork,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    private const double LowConfidenceThreshold = 0.65;

    private static readonly Dictionary<string, int> PriorityRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"]      = 1,
        ["Medium"]   = 2,
        ["High"]     = 3,
        ["Critical"] = 4
    };

    public async Task<GenerateRescueMissionSuggestionResponse> Handle(
        GenerateRescueMissionSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating rescue mission suggestion for ClusterId={clusterId}, RequestedBy={userId}",
            request.ClusterId, request.RequestedByUserId);

        // 1. Load SOS requests + nearby depots (throws NotFoundException / BadRequestException on bad input)
        var context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);

        // 2. Call AI to generate suggestion
        var result = await _suggestionService.GenerateSuggestionAsync(
            context.SosRequests, context.NearbyDepots, context.MultiDepotRecommended, cancellationToken);

        // 3. Post-process: backfill fields AI often leaves null (item_id, sos_request_id)
        if (result.IsSuccess && result.SuggestedActivities.Count > 0)
        {
            BackfillItemIds(result.SuggestedActivities, context.NearbyDepots);
            BackfillSosRequestIds(result.SuggestedActivities, context.SosRequests);
        }

        // 4. Flag low-confidence results for manual review
        if (result.IsSuccess && result.ConfidenceScore < LowConfidenceThreshold)
        {
            result.NeedsManualReview = true;
            result.LowConfidenceWarning =
                $"AI chỉ đạt độ tự tin {result.ConfidenceScore:P0} (ngưỡng: {LowConfidenceThreshold:P0}). " +
                "Kế hoạch có thể chưa chính xác — điều phối viên nên xem xét và điều chỉnh thủ công.";
            _logger.LogWarning(
                "AI low-confidence result for ClusterId={clusterId}: ConfidenceScore={score}",
                request.ClusterId, result.ConfidenceScore);
        }
        result.MultiDepotRecommended = context.MultiDepotRecommended;

        _logger.LogInformation(
            "Rescue mission suggestion result: IsSuccess={isSuccess}, Title={title}, ResponseTime={time}ms, Confidence={conf}, NeedsReview={review}, MultiDepot={multi}",
            result.IsSuccess, result.SuggestedMissionTitle, result.ResponseTimeMs,
            result.ConfidenceScore, result.NeedsManualReview, result.MultiDepotRecommended);

        // 5. Persist to DB (always save, even partial results)
        int? savedSuggestionId = null;
        try
        {
            savedSuggestionId = await PersistSuggestionAsync(request.ClusterId, context.Cluster, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist mission suggestion to DB for ClusterId={clusterId}", request.ClusterId);
        }

        return new GenerateRescueMissionSuggestionResponse
        {
            SuggestionId          = savedSuggestionId,
            IsSuccess             = result.IsSuccess,
            ErrorMessage          = result.ErrorMessage,
            ModelName             = result.ModelName,
            ResponseTimeMs        = result.ResponseTimeMs,
            SosRequestCount       = context.SosRequests.Count,
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType  = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            OverallAssessment     = result.OverallAssessment,
            SuggestedActivities   = result.SuggestedActivities,
            SuggestedResources    = result.SuggestedResources,
            EstimatedDuration     = result.EstimatedDuration,
            SpecialNotes          = result.SpecialNotes,
            ConfidenceScore       = result.ConfidenceScore,
            NeedsManualReview     = result.NeedsManualReview,
            LowConfidenceWarning  = result.LowConfidenceWarning,
            MultiDepotRecommended = result.MultiDepotRecommended
        };
    }

    // --- DB Persistence -----------------------------------------------------------

    private async Task<int> PersistSuggestionAsync(
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
            ClusterId             = clusterId,
            ModelName             = result.ModelName,
            AnalysisType          = "RescueMissionSuggestion",
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            ConfidenceScore       = result.ConfidenceScore,
            Metadata              = metadataJson,
            CreatedAt             = DateTime.UtcNow,
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
        _logger.LogInformation("Saved mission suggestion to DB: SuggestionId={id}", suggestionId);

        cluster.IsMissionCreated = true;
        await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
        await _unitOfWork.SaveAsync();

        return suggestionId;
    }

    // --- Post-Processing Helpers --------------------------------------------------

    private static void BackfillItemIds(List<SuggestedActivityDto> activities, List<DepotSummary> depots)
    {
        var itemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var depot in depots)
            foreach (var inv in depot.Inventories)
                if (inv.ItemId.HasValue && !string.IsNullOrEmpty(inv.ItemName))
                    itemLookup.TryAdd(NormalizeItemName(inv.ItemName), inv.ItemId.Value);

        if (itemLookup.Count == 0) return;

        foreach (var activity in activities)
        {
            if (activity.SuppliesToCollect is null) continue;
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (supply.ItemId.HasValue || string.IsNullOrEmpty(supply.ItemName)) continue;
                var normalized = NormalizeItemName(supply.ItemName);

                if (itemLookup.TryGetValue(normalized, out var exactId))
                {
                    supply.ItemId = exactId;
                    continue;
                }

                foreach (var (key, id) in itemLookup)
                {
                    if (normalized.Contains(key) || key.Contains(normalized))
                    {
                        supply.ItemId = id;
                        break;
                    }
                }
            }
        }
    }

    private static string NormalizeItemName(string name) =>
        name.ToLowerInvariant()
            .Replace("&", " ").Replace("(", " ").Replace(")", " ")
            .Replace(",", " ").Replace("-", " ").Replace("/", " ")
            .Replace("  ", " ").Trim();

    private static readonly Regex CoordRegex =
        new(@"(-?\d{1,3}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)", RegexOptions.Compiled);

    private static void BackfillSosRequestIds(List<SuggestedActivityDto> activities, List<SosRequestSummary> sosRequests)
    {
        if (sosRequests.Count == 0) return;

        if (sosRequests.Count == 1)
        {
            var id = sosRequests[0].Id;
            foreach (var a in activities)
                a.SosRequestId ??= id;
            return;
        }

        var sosWithGps = sosRequests.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
        var fallbackSos = sosRequests
            .OrderByDescending(s => PriorityRank.TryGetValue(s.PriorityLevel ?? string.Empty, out var r) ? r : 0)
            .First();

        foreach (var activity in activities)
        {
            if (activity.SosRequestId.HasValue) continue;

            if (sosWithGps.Count > 0 && !string.IsNullOrEmpty(activity.Description))
            {
                var match = CoordRegex.Match(activity.Description);
                if (match.Success
                    && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lat)
                    && double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lon))
                {
                    var nearest = sosWithGps
                        .OrderBy(s => HaversineKm(lat, lon, s.Latitude!.Value, s.Longitude!.Value))
                        .First();
                    activity.SosRequestId = nearest.Id;
                    continue;
                }
            }

            activity.SosRequestId = fallbackSos.Id;
        }
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
