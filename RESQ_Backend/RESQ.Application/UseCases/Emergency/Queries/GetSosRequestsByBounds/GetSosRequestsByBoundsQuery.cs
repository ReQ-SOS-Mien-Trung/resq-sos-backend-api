using MediatR;
using RESQ.Application.Common.Sorting;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;

public class GetSosRequestsByBoundsQuery : IRequest<List<SosRequestDto>>
{
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
    public List<SosRequestStatus>? Statuses { get; set; }
    public List<SosPriorityLevel>? Priorities { get; set; }
    public List<SosRequestType>? SosTypes { get; set; }
    public IReadOnlyList<SosSortOption>? SortOptions { get; set; }
}
