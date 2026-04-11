using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;

public record GetAlternativeDepotsQuery(int ClusterId, int SelectedDepotId) : IRequest<GetAlternativeDepotsResponse>;
