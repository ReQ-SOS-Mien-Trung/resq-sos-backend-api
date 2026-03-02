using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IRescueMissionSuggestionService suggestionService,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    IDepotRepository depotRepository,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    // Số kho gần nhất tối đa gửi cho AI
    private const int MaxDepotContext = 5;

    // Thứ tự ưu tiên: giá trị càng cao càng quan trọng
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

        // 3. Tìm SOS request quan trọng nhất để tính khoảng cách đến kho
        var anchorSos = FindHighestPrioritySos(sosRequestSummaries);

        // 4. Query DB: lấy các kho đang hoạt động và còn hàng, tính khoảng cách, lọc + sắp xếp
        var nearbyDepots = await BuildNearbyDepotSummariesAsync(anchorSos, cancellationToken);

        _logger.LogInformation(
            "Depot context: AnchorSOS={sosId} (Priority={priority}, Lat={lat}, Lng={lng}), EligibleDepots={count}",
            anchorSos?.Id, anchorSos?.PriorityLevel, anchorSos?.Latitude, anchorSos?.Longitude, nearbyDepots.Count);

        // 5. Call AI to generate suggestion
        var result = await _suggestionService.GenerateSuggestionAsync(sosRequestSummaries, nearbyDepots, cancellationToken);

        _logger.LogInformation(
            "Rescue mission suggestion result: IsSuccess={isSuccess}, Title={title}, ResponseTime={time}ms",
            result.IsSuccess, result.SuggestedMissionTitle, result.ResponseTimeMs);

        // 6. Persist to DB (always save, even partial results)
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

        // 7. Map result to response
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

    /// <summary>
    /// Tìm SOS request có mức ưu tiên cao nhất trong cluster để làm điểm neo tính khoảng cách.
    /// Tiebreaker: chờ lâu nhất (WaitTimeMinutes desc) → tạo sớm nhất (CreatedAt asc).
    /// </summary>
    private static SosRequestSummary? FindHighestPrioritySos(List<SosRequestSummary> sosRequests)
    {
        return sosRequests
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .OrderByDescending(s => PriorityRank.TryGetValue(s.PriorityLevel ?? string.Empty, out var rank) ? rank : 0)
            .ThenByDescending(s => s.WaitTimeMinutes ?? 0)
            .ThenBy(s => s.CreatedAt ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// Query DB lấy danh sách kho đang hoạt động, lọc kho không đủ hàng,
    /// tính khoảng cách Haversine đến SOS quan trọng nhất, sắp xếp theo khoảng cách gần nhất.
    /// Trả về tối đa <see cref="MaxDepotContext"/> kho gần nhất.
    /// </summary>
    private async Task<List<DepotSummary>> BuildNearbyDepotSummariesAsync(
        SosRequestSummary? anchorSos,
        CancellationToken cancellationToken)
    {
        if (anchorSos is null)
        {
            _logger.LogWarning("Không có SOS request nào có toạ độ GPS — bỏ qua context kho tiếp tế.");
            return [];
        }

        // Query DB: chỉ lấy kho Status=Available và CurrentUtilization > 0
        var availableDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();

        if (availableDepots.Count == 0)
        {
            _logger.LogWarning("Không có kho nào đủ điều kiện (Available + còn hàng).");
            return [];
        }

        var depotSummaries = availableDepots
            .Where(d => d.Location is not null) // bỏ kho không có toạ độ
            .Select(d =>
            {
                var distKm = HaversineKm(
                    anchorSos.Latitude!.Value,
                    anchorSos.Longitude!.Value,
                    d.Location!.Latitude,
                    d.Location!.Longitude);

                return new DepotSummary
                {
                    Id = d.Id,
                    Name = d.Name,
                    Address = d.Address,
                    Latitude = d.Location.Latitude,
                    Longitude = d.Location.Longitude,
                    DistanceKm = Math.Round(distKm, 2),
                    Capacity = d.Capacity,
                    CurrentUtilization = d.CurrentUtilization,
                    Status = d.Status.ToString()
                };
            })
            .OrderBy(d => d.DistanceKm)
            .Take(MaxDepotContext)
            .ToList();

        return depotSummaries;
    }

    /// <summary>
    /// Tính khoảng cách Haversine (km) giữa hai toạ độ GPS.
    /// </summary>
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // bán kính Trái Đất (km)
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
