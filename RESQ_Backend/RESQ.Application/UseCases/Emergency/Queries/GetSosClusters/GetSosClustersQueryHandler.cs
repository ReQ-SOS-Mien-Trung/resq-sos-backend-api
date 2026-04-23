using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Enum.Emergency;
namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public class GetSosClustersQueryHandler(
    ISosClusterRepository sosClusterRepository,
    ILogger<GetSosClustersQueryHandler> logger
) : IRequestHandler<GetSosClustersQuery, PagedResult<SosClusterDto>>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ILogger<GetSosClustersQueryHandler> _logger = logger;

    public async Task<PagedResult<SosClusterDto>> Handle(GetSosClustersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSosClustersQuery");

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var statuses = request.Statuses?
            .Distinct()
            .ToArray();
        var priorities = request.Priorities?
            .Distinct()
            .ToArray();
        var sosTypes = request.SosTypes?
            .Distinct()
            .ToArray();

        var pagedClusters = await _sosClusterRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SosRequestId,
            statuses,
            priorities,
            sosTypes,
            cancellationToken);

        var items = pagedClusters.Items
            .Select(c => new SosClusterDto
            {
                Id = c.Id,
                CenterLatitude = c.CenterLatitude,
                CenterLongitude = c.CenterLongitude,
                RadiusKm = c.RadiusKm,
                SeverityLevel = c.SeverityLevel,
                WaterLevel = c.WaterLevel,
                VictimEstimated = c.VictimEstimated,
                ChildrenCount = c.ChildrenCount,
                ElderlyCount = c.ElderlyCount,
                MedicalUrgencyScore = c.MedicalUrgencyScore,
                SosRequestCount = c.SosRequestIds.Count,
                SosRequestIds = c.SosRequestIds,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                LastUpdatedAt = c.LastUpdatedAt
            })
            .ToList();

        return new PagedResult<SosClusterDto>(
            items,
            pagedClusters.TotalCount,
            pagedClusters.PageNumber,
            pagedClusters.PageSize);
    }
}
