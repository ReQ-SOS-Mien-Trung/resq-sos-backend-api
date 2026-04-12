using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotMetadata;

public class GetDepotMetadataHandler : IRequestHandler<GetDepotMetadataQuery, List<MetadataDto>>
{
    private readonly IDepotRepository _depotRepository;

    public GetDepotMetadataHandler(IDepotRepository depotRepository)
    {
        _depotRepository = depotRepository;
    }

    public async Task<List<MetadataDto>> Handle(GetDepotMetadataQuery request, CancellationToken cancellationToken)
    {
        var depots = await _depotRepository.GetAllAsync(cancellationToken);     

        if (request.ExcludeUnavailableAndClosed)
        {
            depots = depots.Where(d => 
                d.Status != DepotStatus.Unavailable && 
                d.Status != DepotStatus.Closed &&
                d.CurrentUtilization < d.Capacity &&
                d.CurrentWeightUtilization < d.WeightCapacity
            ).ToList();
        }

        return depots
            .OrderBy(d => d.Id)
            .Select(d => new MetadataDto
            {
                Key = d.Id.ToString(),
                Value = d.Name ?? string.Empty
            })
            .ToList();
    }
}
