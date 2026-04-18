using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignFundFlowChart;

public class GetCampaignFundFlowChartHandler(
    IFundCampaignRepository campaignRepository,
    IFundTransactionRepository transactionRepository)
    : IRequestHandler<GetCampaignFundFlowChartQuery, CampaignFundFlowChartDto>
{
    private readonly IFundCampaignRepository    _campaignRepository    = campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository = transactionRepository;

    public async Task<CampaignFundFlowChartDto> Handle(
        GetCampaignFundFlowChartQuery request,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(request.CampaignId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chiến dịch với id = {request.CampaignId}.");

        // Apply end-of-day when caller passes only a date (no time component)
        var fromUtc = request.From;
        var toUtc   = request.To.HasValue && request.To.Value.TimeOfDay == TimeSpan.Zero
            ? request.To.Value.Date.AddDays(1).AddTicks(-1)
            : request.To;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new BadRequestException("Ngày bắt đầu không được sau ngày kết thúc.");

        var granularity = request.Granularity?.ToLowerInvariant() switch
        {
            "week" => "week",
            _      => "month"
        };

        var rawPeriods = await _transactionRepository.GetPeriodFundFlowAsync(
            request.CampaignId, fromUtc, toUtc, cancellationToken);

        List<CampaignFundFlowDataPoint> dataPoints;

        if (granularity == "week")
        {
            dataPoints = rawPeriods.Select(p => new CampaignFundFlowDataPoint
            {
                PeriodLabel = $"{p.Period:yyyy}-W{GetIsoWeek(p.Period):D2}",
                TotalIn     = p.TotalIn,
                TotalOut    = p.TotalOut,
                NetBalance  = p.TotalIn - p.TotalOut
            }).ToList();
        }
        else
        {
            dataPoints = new List<CampaignFundFlowDataPoint>();

            if (rawPeriods.Count > 0)
            {
                var lookup = rawPeriods.ToDictionary(p => new DateTime(p.Period.Year, p.Period.Month, 1));

                // Determine month range from actual data when no bounds provided
                var startMonth = fromUtc.HasValue
                    ? new DateTime(fromUtc.Value.Year, fromUtc.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    : new DateTime(rawPeriods.Min(p => p.Period).Year, rawPeriods.Min(p => p.Period).Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var endMonth = toUtc.HasValue
                    ? new DateTime(toUtc.Value.Year, toUtc.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    : new DateTime(rawPeriods.Max(p => p.Period).Year, rawPeriods.Max(p => p.Period).Month, 1, 0, 0, 0, DateTimeKind.Utc);

                for (var cursor = startMonth; cursor <= endMonth; cursor = cursor.AddMonths(1))
                {
                    var hasData = lookup.TryGetValue(cursor, out var period);
                    dataPoints.Add(new CampaignFundFlowDataPoint
                    {
                        PeriodLabel = cursor.ToString("yyyy-MM"),
                        TotalIn     = hasData ? period.TotalIn  : 0m,
                        TotalOut    = hasData ? period.TotalOut : 0m,
                        NetBalance  = hasData ? period.TotalIn - period.TotalOut : 0m
                    });
                }
            }
        }

        return new CampaignFundFlowChartDto
        {
            CampaignId   = campaign.Id,
            CampaignName = campaign.Name,
            Granularity  = granularity,
            From         = fromUtc,
            To           = toUtc,
            DataPoints   = dataPoints
        };
    }

    private static int GetIsoWeek(DateTime dt)
    {
        var day = (int)System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetDayOfWeek(dt);
        return System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetWeekOfYear(dt.AddDays(4 - (day == 0 ? 7 : day)),
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
    }
}
