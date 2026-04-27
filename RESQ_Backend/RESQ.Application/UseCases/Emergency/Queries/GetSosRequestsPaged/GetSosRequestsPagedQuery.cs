using MediatR;
using RESQ.Application.Common.Sorting;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;

public class GetSosRequestsPagedQuery : IRequest<GetSosRequestsPagedResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? SosRequestId { get; set; }
    public List<SosRequestStatus>? Statuses { get; set; }
    public List<SosPriorityLevel>? Priorities { get; set; }
    public List<SosRequestType>? SosTypes { get; set; }
    public IReadOnlyList<SosSortOption>? SortOptions { get; set; }
}
