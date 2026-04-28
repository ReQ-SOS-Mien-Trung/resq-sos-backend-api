using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

public record GetRescuerOverviewQuery(int Months = 12) : IRequest<RescuerOverviewResponse>;
