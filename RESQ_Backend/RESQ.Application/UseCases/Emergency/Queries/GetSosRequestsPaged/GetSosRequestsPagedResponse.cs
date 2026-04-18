namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;

public class GetSosRequestsPagedResponse
{
    public List<SosRequestDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
