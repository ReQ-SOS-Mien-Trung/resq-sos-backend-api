using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotCapacityChart;

public class GetDepotCapacityChartHandler(IDepotRepository depotRepository)
    : IRequestHandler<GetDepotCapacityChartQuery, DepotCapacityChartDto>
{
    private readonly IDepotRepository _depotRepository = depotRepository;

    public async Task<DepotCapacityChartDto> Handle(
        GetDepotCapacityChartQuery request,
        CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với id = {request.DepotId}.");

        var volumePercent = depot.Capacity > 0
            ? Math.Round(depot.CurrentUtilization / depot.Capacity * 100, 2)
            : 0m;

        var weightPercent = depot.WeightCapacity > 0
            ? Math.Round(depot.CurrentWeightUtilization / depot.WeightCapacity * 100, 2)
            : 0m;

        return new DepotCapacityChartDto
        {
            DepotId           = depot.Id,
            DepotName         = depot.Name,
            CurrentVolume     = depot.CurrentUtilization,
            MaxVolume         = depot.Capacity,
            VolumeUsagePercent = volumePercent,
            CurrentWeight     = depot.CurrentWeightUtilization,
            MaxWeight         = depot.WeightCapacity,
            WeightUsagePercent = weightPercent
        };
    }
}
