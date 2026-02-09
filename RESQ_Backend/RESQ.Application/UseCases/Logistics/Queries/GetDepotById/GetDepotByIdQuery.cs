using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotById;

public record GetDepotByIdQuery(int Id) : IRequest<DepotDto>;
