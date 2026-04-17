using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart;

public class GetDepotInventoryMovementChartHandler(
    IDepotRepository depotRepository,
    IInventoryLogRepository inventoryLogRepository)
    : IRequestHandler<GetDepotInventoryMovementChartQuery, DepotInventoryMovementChartDto>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IInventoryLogRepository _inventoryLogRepository = inventoryLogRepository;

    public async Task<DepotInventoryMovementChartDto> Handle(
        GetDepotInventoryMovementChartQuery request,
        CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với id = {request.DepotId}.");

        // If caller provides a date with no time (time == 00:00:00), treat To as end-of-day.
        // If nothing is provided (null), repo returns all data.
        var fromUtc = request.From;
        var toUtc   = request.To.HasValue && request.To.Value.TimeOfDay == TimeSpan.Zero
            ? request.To.Value.Date.AddDays(1).AddTicks(-1)
            : request.To;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new BadRequestException("Ngày bắt đầu không được sau ngày kết thúc.");

        var dataPoints = await _inventoryLogRepository.GetDailyMovementChartAsync(
            request.DepotId, fromUtc, toUtc, cancellationToken);

        return new DepotInventoryMovementChartDto
        {
            DepotId    = depot.Id,
            DepotName  = depot.Name,
            From       = fromUtc,
            To         = toUtc,
            DataPoints = dataPoints
        };
    }
}
