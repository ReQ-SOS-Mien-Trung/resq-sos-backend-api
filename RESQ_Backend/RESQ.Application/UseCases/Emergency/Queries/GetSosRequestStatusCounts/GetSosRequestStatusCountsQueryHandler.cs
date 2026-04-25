using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestStatusCounts;

public class GetSosRequestStatusCountsQueryHandler(
    ISosRequestStatisticsRepository sosRequestStatisticsRepository,
    ILogger<GetSosRequestStatusCountsQueryHandler> logger
) : IRequestHandler<GetSosRequestStatusCountsQuery, GetSosRequestStatusCountsResponse>
{
    public async Task<GetSosRequestStatusCountsResponse> Handle(
        GetSosRequestStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var to = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        var from = request.From?.ToUniversalTime() ?? to.AddMonths(-6);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        logger.LogInformation(
            "GetSosRequestStatusCounts: from={from}, to={to}",
            from,
            to);

        var countsByStatus = await sosRequestStatisticsRepository.GetStatusCountsAsync(
            from,
            to,
            cancellationToken);

        var statusCounts = Enum.GetValues<SosRequestStatus>()
            .Select(status => new SosRequestStatusCountDto
            {
                Status = status.ToString(),
                Count = countsByStatus.TryGetValue(status.ToString(), out var count) ? count : 0
            })
            .ToList();

        return new GetSosRequestStatusCountsResponse
        {
            From = from,
            To = to,
            Total = statusCounts.Sum(item => item.Count),
            StatusCounts = statusCounts
        };
    }
}
