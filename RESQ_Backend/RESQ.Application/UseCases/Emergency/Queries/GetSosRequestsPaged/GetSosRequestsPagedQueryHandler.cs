using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;

public class GetSosRequestsPagedQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ILogger<GetSosRequestsPagedQueryHandler> logger
) : IRequestHandler<GetSosRequestsPagedQuery, GetSosRequestsPagedResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ILogger<GetSosRequestsPagedQueryHandler> _logger = logger;

    public async Task<GetSosRequestsPagedResponse> Handle(GetSosRequestsPagedQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving SOS requests page {page}", nameof(GetSosRequestsPagedQueryHandler), request.PageNumber);

        var pagedResult = await _sosRequestRepository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedResult.Items.Select(x => new SosRequestDto
        {
            Id = x.Id,
            PacketId = x.PacketId,
            ClusterId = x.ClusterId,
            UserId = x.UserId,
            SosType = x.SosType,
            RawMessage = x.RawMessage,
            StructuredData = ParseJson(x.StructuredData),
            NetworkMetadata = ParseJson(x.NetworkMetadata),
            SenderInfo = ParseJson(x.SenderInfo),
            OriginId = x.OriginId,
            Status = x.Status.ToString(),
            PriorityLevel = x.PriorityLevel?.ToString(),
            WaitTimeMinutes = x.WaitTimeMinutes,
            Latitude = x.Location?.Latitude,
            Longitude = x.Location?.Longitude,
            LocationAccuracy = x.LocationAccuracy,
            Timestamp = x.Timestamp,
            CreatedAt = x.CreatedAt,
            LastUpdatedAt = x.LastUpdatedAt,
            ReviewedAt = x.ReviewedAt,
            ReviewedById = x.ReviewedById
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

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return null; }
    }
}
