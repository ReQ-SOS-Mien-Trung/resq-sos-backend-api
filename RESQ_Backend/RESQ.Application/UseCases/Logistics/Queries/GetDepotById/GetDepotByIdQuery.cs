using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotById;

public record GetDepotByIdQuery(int Id) : IRequest<DepotDto>;
