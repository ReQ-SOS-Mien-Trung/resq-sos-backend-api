using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionSuccessRateSummary;

public record GetMissionSuccessRateSummaryQuery : IRequest<GetMissionSuccessRateSummaryResponse>;
