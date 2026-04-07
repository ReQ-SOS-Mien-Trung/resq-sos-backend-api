using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public class CreateSosClusterCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateSosClusterCommandHandler> logger
) : IRequestHandler<CreateSosClusterCommand, CreateSosClusterResponse>
{
    /// <summary>Khoảng cách tối đa (km) giữa 2 SOS request bất kỳ trong cùng một cluster.</summary>
    private const double DefaultMaxClusterSpreadKm = 10.0;

    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateSosClusterCommandHandler> _logger = logger;

    public async Task<CreateSosClusterResponse> Handle(CreateSosClusterCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating SOS cluster with {count} requests, RequestedBy={userId}",
            request.SosRequestIds.Count, request.CreatedByUserId);

        // Validate all SOS requests exist and collect data for auto-calculation
        var resolvedRequests = new List<SosRequestModel>();
        var notFoundIds = new List<int>();

        foreach (var sosId in request.SosRequestIds.Distinct())
        {
            var sos = await _sosRequestRepository.GetByIdAsync(sosId, cancellationToken);
            if (sos is null)
                notFoundIds.Add(sosId);
            else
                resolvedRequests.Add(sos);
        }

        if (notFoundIds.Count > 0)
            throw new NotFoundException($"Không tìm thấy SOS request với ID: {string.Join(", ", notFoundIds)}");

        if (resolvedRequests.Count == 0)
            throw new BadRequestException("Không có SOS request hợp lệ để tạo cluster");

        var clusterGroupingConfig = await _sosClusterGroupingConfigRepository.GetAsync(cancellationToken);
        var maxClusterSpreadKm = clusterGroupingConfig?.MaximumDistanceKm > 0
            ? clusterGroupingConfig.MaximumDistanceKm
            : DefaultMaxClusterSpreadKm;

        // Validate: all SOS requests must be Pending or Incident
        var nonPendingRequests = resolvedRequests
            .Where(r => r.Status is not SosRequestStatus.Pending and not SosRequestStatus.Incident)
            .ToList();
        if (nonPendingRequests.Count > 0)
            throw new BadRequestException(
                $"Chỉ được tạo cluster từ các SOS request ở trạng thái Pending hoặc Incident. " +
                $"Các request không hợp lệ: {string.Join(", ", nonPendingRequests.Select(r => $"#{r.Id} ({r.Status})"))}");

        // Validate: SOS requests must not already belong to another cluster
        var alreadyClusteredRequests = resolvedRequests
            .Where(r => r.ClusterId.HasValue)
            .ToList();
        if (alreadyClusteredRequests.Count > 0)
            throw new ConflictException(
                $"Các SOS request sau đã thuộc cluster khác: " +
                $"{string.Join(", ", alreadyClusteredRequests.Select(r => $"#{r.Id} (Cluster #{r.ClusterId})"))}");

        // Validate: no two SOS requests may be farther apart than configured max spread distance
        var withCoords = resolvedRequests
            .Where(r => r.Location != null)
            .ToList();

        for (int i = 0; i < withCoords.Count; i++)
        {
            for (int j = i + 1; j < withCoords.Count; j++)
            {
                var a = withCoords[i];
                var b = withCoords[j];
                double distKm = HaversineKm(
                    a.Location!.Latitude, a.Location.Longitude,
                    b.Location!.Latitude, b.Location.Longitude);

                if (distKm > maxClusterSpreadKm)
                    throw new BadRequestException(
                        $"SOS request #{a.Id} và #{b.Id} cách nhau {distKm:F1} km, " +
                        $"vượt quá giới hạn {maxClusterSpreadKm:F1} km cho phép trong một cluster. " +
                        $"Vui lòng chỉ nhóm các yêu cầu trong cùng khu vực địa lý.");
            }
        }

        // Auto-calculate center coordinates from average of all SOS request locations
        var validCoords = resolvedRequests
            .Where(r => r.Location?.Latitude != null && r.Location?.Longitude != null)
            .ToList();
        double? centerLat = validCoords.Count > 0 ? validCoords.Average(r => r.Location!.Latitude) : null;
        double? centerLon = validCoords.Count > 0 ? validCoords.Average(r => r.Location!.Longitude) : null;

        // Auto-calculate severity from highest priority level among SOS requests
        var highestPriority = resolvedRequests
            .Where(r => r.PriorityLevel.HasValue)
            .Select(r => r.PriorityLevel!.Value)
            .DefaultIfEmpty()
            .Max();
        string? severityLevel = resolvedRequests.Any(r => r.PriorityLevel.HasValue)
            ? highestPriority.ToString()
            : null;

        // Auto-calculate people counts from StructuredData JSON
        int totalAdult = 0, totalChild = 0, totalElderly = 0;
        bool hasPeopleCount = false;
        foreach (var sos in resolvedRequests)
        {
            if (string.IsNullOrWhiteSpace(sos.StructuredData)) continue;
            try
            {
                var doc = JsonDocument.Parse(sos.StructuredData);
                // Dual-read: try new nested format first, fallback to old flat
                JsonElement pc;
                bool hasPc = (doc.RootElement.TryGetProperty("incident", out var incident)
                              && incident.TryGetProperty("people_count", out pc))
                             || doc.RootElement.TryGetProperty("people_count", out pc);
                if (hasPc)
                {
                    hasPeopleCount = true;
                    if (pc.TryGetProperty("adult", out var a) && a.ValueKind == JsonValueKind.Number)
                        totalAdult += a.GetInt32();
                    if (pc.TryGetProperty("child", out var c) && c.ValueKind == JsonValueKind.Number)
                        totalChild += c.GetInt32();
                    if (pc.TryGetProperty("elderly", out var e) && e.ValueKind == JsonValueKind.Number)
                        totalElderly += e.GetInt32();
                }
            }
            catch (JsonException) { /* skip malformed JSON */ }
        }

        int? victimEstimated = hasPeopleCount ? totalAdult + totalChild + totalElderly : null;
        int? childrenCount = hasPeopleCount ? totalChild : null;
        int? elderlyCount = hasPeopleCount ? totalElderly : null;

        // Create cluster
        var cluster = new SosClusterModel
        {
            CenterLatitude = centerLat,
            CenterLongitude = centerLon,
            SeverityLevel = severityLevel,
            WaterLevel = null,
            VictimEstimated = victimEstimated,
            ChildrenCount = childrenCount,
            ElderlyCount = elderlyCount,
            MedicalUrgencyScore = null,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            SosRequestIds = resolvedRequests.Select(r => r.Id).ToList()
        };

        var clusterId = await _sosClusterRepository.CreateAsync(cluster, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("SOS cluster created successfully: ClusterId={clusterId}", clusterId);

        return new CreateSosClusterResponse
        {
            ClusterId = clusterId,
            SosRequestCount = resolvedRequests.Count,
            SosRequestIds = resolvedRequests.Select(r => r.Id).ToList(),
            SeverityLevel = severityLevel,
            CreatedAt = cluster.CreatedAt
        };
    }

    /// <summary>
    /// Tính khoảng cách (km) giữa hai toạ độ GPS theo công thức Haversine.
    /// </summary>
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // bán kính Trái Đất (km)
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
