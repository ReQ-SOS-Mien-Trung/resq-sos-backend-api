using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

public class AddSosRequestToClusterCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository,
    IUnitOfWork unitOfWork,
    ISosRequestRealtimeHubService sosRequestRealtimeHubService,
    ILogger<AddSosRequestToClusterCommandHandler> logger)
    : IRequestHandler<AddSosRequestToClusterCommand, AddSosRequestToClusterResponse>
{
    private const double DefaultMaxClusterSpreadKm = 10.0;

    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ISosRequestRealtimeHubService _sosRequestRealtimeHubService = sosRequestRealtimeHubService;
    private readonly ILogger<AddSosRequestToClusterCommandHandler> _logger = logger;

    public async Task<AddSosRequestToClusterResponse> Handle(
        AddSosRequestToClusterCommand request,
        CancellationToken cancellationToken)
    {
        var requestedIds = request.SosRequestIds?.ToList() ?? [];
        ValidateRequestedSosRequestIds(requestedIds);

        _logger.LogInformation(
            "Adding {SosRequestCount} SOS requests to ClusterId={ClusterId}, RequestedBy={UserId}",
            requestedIds.Count,
            request.ClusterId,
            request.RequestedByUserId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        if (cluster.Status is not SosClusterStatus.Pending and not SosClusterStatus.Suggested)
        {
            throw new ConflictException(
                "Chỉ được thêm SOS request vào cluster ở trạng thái Pending hoặc Suggested. " +
                $"Cluster #{request.ClusterId} hiện đang ở trạng thái {cluster.Status}.");
        }

        var incomingRequests = await GetIncomingSosRequestsAsync(requestedIds, cancellationToken);
        ValidateAllRequestedSosRequestsExist(requestedIds, incomingRequests);
        ValidateIncomingSosRequestStatuses(incomingRequests);
        ValidateIncomingSosRequestClusterMembership(request.ClusterId, incomingRequests);

        var clusterRequests = (await _sosRequestRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken))
            .ToList();
        var updatedRequests = clusterRequests
            .Concat(incomingRequests)
            .ToList();

        ValidateClusterRequestCount(updatedRequests);

        var clusterGroupingConfig = await _sosClusterGroupingConfigRepository.GetAsync(cancellationToken);
        var maxClusterSpreadKm = clusterGroupingConfig?.MaximumDistanceKm > 0
            ? clusterGroupingConfig.MaximumDistanceKm
            : DefaultMaxClusterSpreadKm;

        ValidateClusterSpread(updatedRequests, maxClusterSpreadKm);

        var aggregate = SosClusterAggregateBuilder.Build(updatedRequests);
        ValidateClusterPeopleCount(aggregate);
        var now = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var sosRequest in incomingRequests)
            {
                sosRequest.ClusterId = request.ClusterId;
                sosRequest.LastUpdatedAt = now;
                await _sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);
            }

            SosClusterAggregateBuilder.ApplyToCluster(cluster, aggregate);
            cluster.LastUpdatedAt = now;
            if (cluster.Status == SosClusterStatus.Suggested)
            {
                cluster.Status = SosClusterStatus.Pending;
            }

            await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
            await _unitOfWork.SaveAsync();
        });

        await _sosRequestRealtimeHubService.PushSosRequestUpdatesAsync(
            incomingRequests.Select(sosRequest => sosRequest.Id),
            "ClusterAssigned",
            notifyUnclustered: true,
            cancellationToken: cancellationToken);

        return new AddSosRequestToClusterResponse
        {
            ClusterId = request.ClusterId,
            AddedSosRequestIds = requestedIds,
            UpdatedCluster = ToDto(cluster)
        };
    }

    private async Task<List<SosRequestModel>> GetIncomingSosRequestsAsync(
        IReadOnlyList<int> sosRequestIds,
        CancellationToken cancellationToken)
    {
        List<SosRequestModel> fetchedRequests;
        if (_sosRequestRepository is ISosRequestBulkReadRepository bulkReadRepository)
        {
            fetchedRequests = await bulkReadRepository.GetByIdsAsync(sosRequestIds, cancellationToken);
        }
        else
        {
            fetchedRequests = [];
            foreach (var sosRequestId in sosRequestIds)
            {
                var sosRequest = await _sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
                if (sosRequest is not null)
                {
                    fetchedRequests.Add(sosRequest);
                }
            }
        }

        var lookup = fetchedRequests
            .GroupBy(sosRequest => sosRequest.Id)
            .ToDictionary(group => group.Key, group => group.First());

        return sosRequestIds
            .Where(lookup.ContainsKey)
            .Select(sosRequestId => lookup[sosRequestId])
            .ToList();
    }

    private static void ValidateRequestedSosRequestIds(IReadOnlyList<int> sosRequestIds)
    {
        if (sosRequestIds.Count == 0)
        {
            throw new BadRequestException("Phải chọn ít nhất một SOS request để thêm vào cluster.");
        }

        var invalidIds = sosRequestIds
            .Where(id => id <= 0)
            .ToList();
        if (invalidIds.Count > 0)
        {
            throw new BadRequestException($"Danh sách SOS request có ID không hợp lệ: {string.Join(", ", invalidIds)}.");
        }

        var duplicateIds = sosRequestIds
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            throw new BadRequestException($"Danh sách SOS request không được có ID trùng lặp: {string.Join(", ", duplicateIds)}.");
        }
    }

    private static void ValidateAllRequestedSosRequestsExist(
        IReadOnlyList<int> requestedIds,
        IReadOnlyCollection<SosRequestModel> resolvedRequests)
    {
        var resolvedIds = resolvedRequests
            .Select(sosRequest => sosRequest.Id)
            .ToHashSet();
        var missingIds = requestedIds
            .Where(id => !resolvedIds.Contains(id))
            .ToList();

        if (missingIds.Count > 0)
        {
            throw new NotFoundException($"Không tìm thấy SOS request với ID: {string.Join(", ", missingIds)}");
        }
    }

    private static void ValidateIncomingSosRequestStatuses(IReadOnlyCollection<SosRequestModel> sosRequests)
    {
        var invalidRequests = sosRequests
            .Where(sosRequest => sosRequest.Status is not SosRequestStatus.Pending and not SosRequestStatus.Incident)
            .ToList();

        if (invalidRequests.Count > 0)
        {
            throw new BadRequestException(
                "Chỉ được thêm các SOS request ở trạng thái Pending hoặc Incident. " +
                $"Các request không hợp lệ: {string.Join(", ", invalidRequests.Select(r => $"#{r.Id} ({r.Status})"))}");
        }
    }

    private static void ValidateIncomingSosRequestClusterMembership(
        int clusterId,
        IReadOnlyCollection<SosRequestModel> sosRequests)
    {
        var alreadyInCurrentCluster = sosRequests
            .Where(sosRequest => sosRequest.ClusterId == clusterId)
            .ToList();
        if (alreadyInCurrentCluster.Count > 0)
        {
            throw new ConflictException(
                $"Các SOS request sau đã thuộc cluster #{clusterId}: " +
                $"{string.Join(", ", alreadyInCurrentCluster.Select(r => $"#{r.Id}"))}.");
        }

        var alreadyInOtherCluster = sosRequests
            .Where(sosRequest => sosRequest.ClusterId.HasValue && sosRequest.ClusterId != clusterId)
            .ToList();
        if (alreadyInOtherCluster.Count > 0)
        {
            throw new ConflictException(
                "Các SOS request sau đã thuộc cluster khác: " +
                $"{string.Join(", ", alreadyInOtherCluster.Select(r => $"#{r.Id} (Cluster #{r.ClusterId})"))}.");
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

    private static void ValidateClusterPeopleCount(SosClusterAggregateSnapshot aggregate)
    {
        if (aggregate.VictimEstimated > SosClusterCapacityLimits.MaxVictimEstimated)
        {
            throw new BadRequestException(
                $"Một cluster chỉ có thể chứa tối đa {SosClusterCapacityLimits.MaxVictimEstimated} người. " +
                $"Tổng số người hiện tại: {aggregate.VictimEstimated}.");
        }
    }

    private static void ValidateClusterSpread(
        IReadOnlyList<SosRequestModel> sosRequests,
        double maxClusterSpreadKm)
    {
        var withCoords = sosRequests
            .Where(sosRequest => sosRequest.Location != null)
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
                {
                    throw new BadRequestException(
                        $"SOS request #{a.Id} và #{b.Id} cách nhau {distKm:F1} km, " +
                        $"vượt quá giới hạn {maxClusterSpreadKm:F1} km cho phép trong một cluster. " +
                        "Vui lòng chỉ nhóm các yêu cầu trong cùng khu vực địa lý.");
                }
            }
        }
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static SosClusterDto ToDto(SosClusterModel cluster)
    {
        return new SosClusterDto
        {
            Id = cluster.Id,
            CenterLatitude = cluster.CenterLatitude,
            CenterLongitude = cluster.CenterLongitude,
            RadiusKm = cluster.RadiusKm,
            SeverityLevel = cluster.SeverityLevel,
            WaterLevel = cluster.WaterLevel,
            VictimEstimated = cluster.VictimEstimated,
            ChildrenCount = cluster.ChildrenCount,
            ElderlyCount = cluster.ElderlyCount,
            MedicalUrgencyScore = cluster.MedicalUrgencyScore,
            SosRequestCount = cluster.SosRequestIds.Count,
            SosRequestIds = cluster.SosRequestIds,
            Status = cluster.Status,
            CreatedAt = cluster.CreatedAt,
            LastUpdatedAt = cluster.LastUpdatedAt
        };
    }
}
