using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMultiLineChart;

public class GetDepotFundMultiLineChartHandler(
    IDepotRepository depotRepository,
    IDepotFundRepository depotFundRepository)
    : IRequestHandler<GetDepotFundMultiLineChartQuery, DepotFundMultiLineChartDto>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotFundRepository _depotFundRepository = depotFundRepository;

    public async Task<DepotFundMultiLineChartDto> Handle(
        GetDepotFundMultiLineChartQuery request,
        CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với id = {request.DepotId}.");

        var toUtc = request.To.HasValue && request.To.Value.TimeOfDay == TimeSpan.Zero
            ? request.To.Value.Date.AddDays(1).AddTicks(-1)
            : request.To;

        if (request.From.HasValue && toUtc.HasValue && request.From > toUtc)
            throw new BadRequestException("Ngày bắt đầu không được sau ngày kết thúc.");

        var series = await _depotFundRepository.GetDailyFundMovementPerFundAsync(
            request.DepotId, request.From, toUtc, cancellationToken);

        return new DepotFundMultiLineChartDto
        {
            DepotId   = depot.Id,
            DepotName = depot.Name,
            From      = request.From,
            To        = toUtc,
            Series    = series
                .Select(s => new DepotFundLineSeries
                {
                    FundId         = s.FundId,
                    FundSourceName = s.FundSourceName,
                    CurrentBalance = s.CurrentBalance,
                    DataPoints     = s.DataPoints
                        .Select(p => new FundLineDataPoint
                        {
                            Date     = p.Date,
                            TotalIn  = p.TotalIn,
                            TotalOut = p.TotalOut
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
