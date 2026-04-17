using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public class GetSosClustersQueryHandler(
    ISosClusterRepository sosClusterRepository,
    ILogger<GetSosClustersQueryHandler> logger
) : IRequestHandler<GetSosClustersQuery, GetSosClustersResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ILogger<GetSosClustersQueryHandler> _logger = logger;

    public async Task<GetSosClustersResponse> Handle(GetSosClustersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSosClustersQuery");

        var clusters = await _sosClusterRepository.GetAllAsync(cancellationToken);

        return new GetSosClustersResponse
        {
            Clusters = clusters.Select(c => new SosClusterDto
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
            }).ToList()
        };
    }
}
