using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;

public class RemoveSosRequestFromClusterCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IClusterAiHistoryRepository clusterAiHistoryRepository,
    IUnitOfWork unitOfWork,
    ISosRequestRealtimeHubService sosRequestRealtimeHubService,
    ILogger<RemoveSosRequestFromClusterCommandHandler> logger)
    : IRequestHandler<RemoveSosRequestFromClusterCommand, RemoveSosRequestFromClusterResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IClusterAiHistoryRepository _clusterAiHistoryRepository = clusterAiHistoryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ISosRequestRealtimeHubService _sosRequestRealtimeHubService = sosRequestRealtimeHubService;
    private readonly ILogger<RemoveSosRequestFromClusterCommandHandler> _logger = logger;

    public async Task<RemoveSosRequestFromClusterResponse> Handle(
        RemoveSosRequestFromClusterCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Removing SosRequestId={SosRequestId} from ClusterId={ClusterId}, RequestedBy={UserId}",
            request.SosRequestId,
            request.ClusterId,
            request.RequestedByUserId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS request với ID: {request.SosRequestId}");

        if (sosRequest.ClusterId != request.ClusterId)
        {
            throw new BadRequestException(
                $"SOS request #{request.SosRequestId} không thuộc cluster #{request.ClusterId}.");
        }

        if (cluster.Status is not SosClusterStatus.Pending and not SosClusterStatus.Suggested)
        {
            throw new ConflictException(
                $"Chỉ được tách SOS request khỏi cluster ở trạng thái Pending hoặc Suggested. " +
                $"Cluster #{request.ClusterId} hiện đang ở trạng thái {cluster.Status}.");
        }

        var clusterRequests = (await _sosRequestRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken))
            .ToList();
        var remainingRequests = clusterRequests
            .Where(existingRequest => existingRequest.Id != request.SosRequestId)
            .ToList();

        var now = DateTime.UtcNow;
        var isClusterDeleted = false;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            sosRequest.ClusterId = null;
            sosRequest.LastUpdatedAt = now;
            await _sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);

            if (remainingRequests.Count == 0)
            {
                await _clusterAiHistoryRepository.DeleteByClusterIdAsync(request.ClusterId, cancellationToken);
                await _sosClusterRepository.DeleteAsync(request.ClusterId, cancellationToken);
                isClusterDeleted = true;
            }
            else
            {
                var aggregate = SosClusterAggregateBuilder.Build(remainingRequests);
                SosClusterAggregateBuilder.ApplyToCluster(cluster, aggregate);
                cluster.LastUpdatedAt = now;
                if (cluster.Status == SosClusterStatus.Suggested)
                {
                    cluster.Status = SosClusterStatus.Pending;
                }

                await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
            }

            await _unitOfWork.SaveAsync();
        });

        await _sosRequestRealtimeHubService.PushSosRequestUpdateAsync(
            request.SosRequestId,
            "ClusterRemoved",
            previousClusterId: request.ClusterId,
            cancellationToken: cancellationToken);

        return new RemoveSosRequestFromClusterResponse
        {
            ClusterId = request.ClusterId,
            RemovedSosRequestId = request.SosRequestId,
            IsClusterDeleted = isClusterDeleted,
            UpdatedCluster = isClusterDeleted ? null : ToDto(cluster)
        };
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
