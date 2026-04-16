using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuersDailyStatistics;

public record GetRescuersDailyStatisticsQuery : IRequest<GetRescuersDailyStatisticsResponse>;
