using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Sorting;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;

public class GetSosRequestsByBoundsQueryHandler(
    ISosRequestMapReadRepository sosRequestMapReadRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ILogger<GetSosRequestsByBoundsQueryHandler> logger
) : IRequestHandler<GetSosRequestsByBoundsQuery, List<SosRequestDto>>
{
    private readonly ISosRequestMapReadRepository _sosRequestMapReadRepository = sosRequestMapReadRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ILogger<GetSosRequestsByBoundsQueryHandler> _logger = logger;

    public async Task<List<SosRequestDto>> Handle(GetSosRequestsByBoundsQuery request, CancellationToken cancellationToken)
    {
        var normalizedStatuses = request.Statuses?
            .Distinct()
            .ToArray();
        var normalizedPriorities = request.Priorities?
            .Distinct()
            .ToArray();
        var normalizedSosTypes = request.SosTypes?
            .Distinct()
            .ToArray();
        var sortOptions = SosSortParser.Normalize(request.SortOptions);

        _logger.LogInformation(
            "Handling {handler} - retrieving SOS requests within bounds ({minLat}, {maxLat}, {minLng}, {maxLng})",
            nameof(GetSosRequestsByBoundsQueryHandler),
            request.MinLat,
            request.MaxLat,
            request.MinLng,
            request.MaxLng);

        var sosRequests = await _sosRequestMapReadRepository.GetByBoundsAsync(
            request.MinLat!.Value,
            request.MaxLat!.Value,
            request.MinLng!.Value,
            request.MaxLng!.Value,
            normalizedStatuses,
            normalizedPriorities,
            normalizedSosTypes,
            sortOptions,
            cancellationToken);

        var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            sosRequests.Select(x => x.Id),
            cancellationToken);
        var incidentLookup = await _sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(
            sosRequests.Select(x => x.Id),
            cancellationToken);

        var dtos = sosRequests.Select(x =>
        {
            victimUpdateLookup.TryGetValue(x.Id, out var latestVictimUpdate);
            var effectiveSosRequest = SosRequestVictimUpdateOverlay.Apply(x, latestVictimUpdate);

            incidentLookup.TryGetValue(x.Id, out var incidents);
            var latestIncident = incidents?.FirstOrDefault();

            return new SosRequestDto
            {
                Id = effectiveSosRequest.Id,
                PacketId = effectiveSosRequest.PacketId,
                ClusterId = effectiveSosRequest.ClusterId,
                UserId = effectiveSosRequest.UserId,
                SosType = effectiveSosRequest.SosType,
                RawMessage = effectiveSosRequest.RawMessage,
                StructuredData = SosStructuredDataParser.Parse(effectiveSosRequest.StructuredData),
                NetworkMetadata = ParseJson<SosNetworkMetadataDto>(effectiveSosRequest.NetworkMetadata),
                SenderInfo = ParseJson<SosSenderInfoDto>(effectiveSosRequest.SenderInfo),
                ReporterInfo = SosStructuredDataParser.ParseReporterInfo(effectiveSosRequest.ReporterInfo, effectiveSosRequest.SenderInfo),
                VictimInfo = ParseJson<SosVictimInfoDto>(effectiveSosRequest.VictimInfo),
                IsSentOnBehalf = effectiveSosRequest.IsSentOnBehalf,
                OriginId = effectiveSosRequest.OriginId,
                Status = effectiveSosRequest.Status.ToString(),
                PriorityLevel = effectiveSosRequest.PriorityLevel?.ToString(),
                Latitude = effectiveSosRequest.Location?.Latitude,
                Longitude = effectiveSosRequest.Location?.Longitude,
                LocationAccuracy = effectiveSosRequest.LocationAccuracy,
                Timestamp = effectiveSosRequest.Timestamp,
                CreatedAt = effectiveSosRequest.CreatedAt,
                ReceivedAt = effectiveSosRequest.ReceivedAt,
                LastUpdatedAt = effectiveSosRequest.LastUpdatedAt,
                ReviewedAt = effectiveSosRequest.ReviewedAt,
                ReviewedById = effectiveSosRequest.ReviewedById,
                CreatedByCoordinatorId = effectiveSosRequest.CreatedByCoordinatorId,
                LatestIncidentNote = latestIncident?.Note,
                LatestIncidentAt = latestIncident?.CreatedAt
            };
        }).ToList();

        _logger.LogInformation(
            "{handler} - retrieved {count} SOS requests within bounds",
            nameof(GetSosRequestsByBoundsQueryHandler),
            dtos.Count);

        return dtos;
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}
