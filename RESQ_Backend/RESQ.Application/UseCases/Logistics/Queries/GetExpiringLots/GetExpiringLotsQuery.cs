using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetExpiringLots;

public record GetExpiringLotsQuery : IRequest<List<ExpiringLotDto>>
{
    public Guid UserId { get; set; }
    public int? DepotId { get; set; }
    public int DaysAhead { get; set; } = 30;
}
