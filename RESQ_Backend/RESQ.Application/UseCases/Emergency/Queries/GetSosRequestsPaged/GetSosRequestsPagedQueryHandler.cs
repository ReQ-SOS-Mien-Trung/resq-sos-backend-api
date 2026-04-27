using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Sorting;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;

public class GetSosRequestsPagedQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ILogger<GetSosRequestsPagedQueryHandler> logger
) : IRequestHandler<GetSosRequestsPagedQuery, GetSosRequestsPagedResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ILogger<GetSosRequestsPagedQueryHandler> _logger = logger;

    public async Task<GetSosRequestsPagedResponse> Handle(GetSosRequestsPagedQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving SOS requests page {page}", nameof(GetSosRequestsPagedQueryHandler), request.PageNumber);

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

        var pagedResult = await _sosRequestRepository.GetAllPagedAsync(
            request.PageNumber,
            request.PageSize,
            normalizedStatuses,
            normalizedPriorities,
            normalizedSosTypes,
            sortOptions,
            request.SosRequestId,
            cancellationToken);
        var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            pagedResult.Items.Select(x => x.Id),
            cancellationToken);
        var incidentLookup = await _sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(
            pagedResult.Items.Select(x => x.Id),
            cancellationToken);

        var dtos = pagedResult.Items.Select(x =>
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

        var response = new GetSosRequestsPagedResponse
        {
            Items = dtos,
            PageNumber = pagedResult.PageNumber,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage
        };

        _logger.LogInformation("{handler} - retrieved {count} SOS requests on page {page}", nameof(GetSosRequestsPagedQueryHandler), dtos.Count, request.PageNumber);
        return response;
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}
