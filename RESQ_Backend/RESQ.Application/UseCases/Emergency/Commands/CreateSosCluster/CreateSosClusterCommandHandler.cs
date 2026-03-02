using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public class CreateSosClusterCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateSosClusterCommandHandler> logger
) : IRequestHandler<CreateSosClusterCommand, CreateSosClusterResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
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
                if (doc.RootElement.TryGetProperty("people_count", out var pc))
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
}
