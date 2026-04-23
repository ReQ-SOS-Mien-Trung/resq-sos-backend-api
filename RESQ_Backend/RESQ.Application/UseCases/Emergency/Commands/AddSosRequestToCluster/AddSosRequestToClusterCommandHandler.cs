using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
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
    ILogger<AddSosRequestToClusterCommandHandler> logger)
    : IRequestHandler<AddSosRequestToClusterCommand, AddSosRequestToClusterResponse>
{
    /// <summary>Khoảng cách tối đa (km) giữa 2 SOS request bất kỳ trong cùng một cluster.</summary>
    private const double DefaultMaxClusterSpreadKm = 10.0;

    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AddSosRequestToClusterCommandHandler> _logger = logger;

    public async Task<AddSosRequestToClusterResponse> Handle(
        AddSosRequestToClusterCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Adding SosRequestId={SosRequestId} to ClusterId={ClusterId}, RequestedBy={UserId}",
            request.SosRequestId,
            request.ClusterId,
            request.RequestedByUserId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS request với ID: {request.SosRequestId}");

        if (cluster.Status is not SosClusterStatus.Pending and not SosClusterStatus.Suggested)
        {
            throw new ConflictException(
                $"Chỉ được thêm SOS request vào cluster ở trạng thái Pending hoặc Suggested. " +
                $"Cluster #{request.ClusterId} hiện đang ở trạng thái {cluster.Status}.");
        }

        if (sosRequest.Status is not SosRequestStatus.Pending and not SosRequestStatus.Incident)
        {
            throw new BadRequestException(
                $"Chỉ được thêm các SOS request ở trạng thái Pending hoặc Incident. " +
                $"SOS request #{request.SosRequestId} hiện đang ở trạng thái {sosRequest.Status}.");
        }

        if (sosRequest.ClusterId.HasValue)
        {
            if (sosRequest.ClusterId.Value == request.ClusterId)
            {
                throw new ConflictException(
                    $"SOS request #{request.SosRequestId} đã thuộc cluster #{request.ClusterId}.");
            }

            throw new ConflictException(
                $"SOS request #{request.SosRequestId} đã thuộc cluster khác (Cluster #{sosRequest.ClusterId.Value}).");
        }

        var clusterRequests = (await _sosRequestRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken))
            .ToList();
        var updatedRequests = clusterRequests
            .Append(sosRequest)
            .ToList();

        var clusterGroupingConfig = await _sosClusterGroupingConfigRepository.GetAsync(cancellationToken);
        var maxClusterSpreadKm = clusterGroupingConfig?.MaximumDistanceKm > 0
            ? clusterGroupingConfig.MaximumDistanceKm
            : DefaultMaxClusterSpreadKm;

        ValidateClusterSpread(updatedRequests, maxClusterSpreadKm);

        var aggregate = SosClusterAggregateBuilder.Build(updatedRequests);
        var now = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            sosRequest.ClusterId = request.ClusterId;
            sosRequest.LastUpdatedAt = now;
            await _sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);

            SosClusterAggregateBuilder.ApplyToCluster(cluster, aggregate);
            cluster.LastUpdatedAt = now;
            if (cluster.Status == SosClusterStatus.Suggested)
            {
                cluster.Status = SosClusterStatus.Pending;
            }

            await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
            await _unitOfWork.SaveAsync();
        });

        return new AddSosRequestToClusterResponse
        {
            ClusterId = request.ClusterId,
            AddedSosRequestId = request.SosRequestId,
            UpdatedCluster = ToDto(cluster)
        };
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
                        $"Vui lòng chỉ nhóm các yêu cầu trong cùng khu vực địa lý.");
                }
            }
        }
    }

    /// <summary>
    /// Tính khoảng cách (km) giữa hai toạ độ GPS theo công thức Haversine.
    /// </summary>
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
