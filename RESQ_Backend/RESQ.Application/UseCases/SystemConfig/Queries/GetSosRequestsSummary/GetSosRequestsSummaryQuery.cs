using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosRequestsSummary;

public record GetSosRequestsSummaryQuery : IRequest<GetSosRequestsSummaryResponse>;
