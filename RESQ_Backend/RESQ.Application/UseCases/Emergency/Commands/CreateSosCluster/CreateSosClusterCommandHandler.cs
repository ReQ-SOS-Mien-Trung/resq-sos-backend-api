using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public class CreateSosClusterCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository,
    IAdminRealtimeHubService adminRealtimeHubService,
    IUnitOfWork unitOfWork,
    ILogger<CreateSosClusterCommandHandler> logger
) : IRequestHandler<CreateSosClusterCommand, CreateSosClusterResponse>
{
    /// <summary>Khoảng cách tối đa (km) giữa 2 SOS request bất kỳ trong cùng một cluster.</summary>
    private const double DefaultMaxClusterSpreadKm = 10.0;

    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
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

        ValidateClusterRequestCount(resolvedRequests);

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

        var aggregate = SosClusterAggregateBuilder.Build(resolvedRequests);
        ValidateClusterCapacity(aggregate);

        // Create cluster
        var cluster = new SosClusterModel
        {
            CenterLatitude = aggregate.CenterLatitude,
            CenterLongitude = aggregate.CenterLongitude,
            SeverityLevel = aggregate.SeverityLevel,
            WaterLevel = null,
            VictimEstimated = aggregate.VictimEstimated,
            ChildrenCount = aggregate.ChildrenCount,
            ElderlyCount = aggregate.ElderlyCount,
            MedicalUrgencyScore = null,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Status = SosClusterStatus.Pending,
            SosRequestIds = aggregate.SosRequestIds
        };

        var clusterId = await _sosClusterRepository.CreateAsync(cluster, cancellationToken);
        await _unitOfWork.SaveAsync();
        await _adminRealtimeHubService.PushSOSClusterUpdateAsync(
            new RESQ.Application.Common.Models.AdminSOSClusterRealtimeUpdate
            {
                EntityId = clusterId,
                EntityType = "SOSCluster",
                ClusterId = clusterId,
                Action = "Created",
                Status = cluster.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        _logger.LogInformation("SOS cluster created successfully: ClusterId={clusterId}", clusterId);

        return new CreateSosClusterResponse
        {
            ClusterId = clusterId,
            SosRequestCount = resolvedRequests.Count,
            SosRequestIds = resolvedRequests.Select(r => r.Id).ToList(),
            SeverityLevel = aggregate.SeverityLevel,
            CreatedAt = cluster.CreatedAt
        };
    }

    private static void ValidateClusterCapacity(SosClusterAggregateSnapshot aggregate)
    {
        if (aggregate.VictimEstimated > SosClusterCapacityLimits.MaxVictimEstimated)
        {
            throw new BadRequestException(
                $"Một cluster chỉ có thể chứa tối đa {SosClusterCapacityLimits.MaxVictimEstimated} người. " +
                $"Tổng số người hiện tại: {aggregate.VictimEstimated}.");
        }
    }

    private static void ValidateClusterRequestCount(IReadOnlyCollection<SosRequestModel> sosRequests)
    {
        var uniqueRequestCount = sosRequests
            .Select(sosRequest => sosRequest.Id)
            .Distinct()
            .Count();

        if (uniqueRequestCount > SosClusterCapacityLimits.MaxSosRequests)
        {
            throw new BadRequestException(
                $"Một cluster chỉ có thể chứa tối đa {SosClusterCapacityLimits.MaxSosRequests} SOS request. " +
                $"Tổng số SOS request hiện tại: {uniqueRequestCount}.");
        }
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
