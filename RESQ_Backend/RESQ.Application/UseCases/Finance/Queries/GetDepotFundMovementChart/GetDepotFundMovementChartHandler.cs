using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart;

public class GetDepotFundMovementChartHandler(
    IDepotRepository depotRepository,
    IDepotFundRepository depotFundRepository)
    : IRequestHandler<GetDepotFundMovementChartQuery, DepotFundMovementChartDto>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotFundRepository _depotFundRepository = depotFundRepository;

    public async Task<DepotFundMovementChartDto> Handle(
        GetDepotFundMovementChartQuery request,
        CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với id = {request.DepotId}.");

        var fromUtc = request.From;
        var toUtc   = request.To.HasValue && request.To.Value.TimeOfDay == TimeSpan.Zero
            ? request.To.Value.Date.AddDays(1).AddTicks(-1)
            : request.To;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new BadRequestException("Ngày bắt đầu không được sau ngày kết thúc.");

        var dataPoints = await _depotFundRepository.GetDailyFundMovementChartAsync(
            request.DepotId, fromUtc, toUtc, cancellationToken);

        return new DepotFundMovementChartDto
        {
            DepotId    = depot.Id,
            DepotName  = depot.Name,
            From       = fromUtc,
            To         = toUtc,
            DataPoints = dataPoints
        };
    }
}
