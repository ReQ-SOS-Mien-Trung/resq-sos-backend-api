using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;

public class GetSosRequestsByBoundsQuery : IRequest<List<SosRequestDto>>
{
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
    public List<string> Statuses { get; set; } = [];
}
