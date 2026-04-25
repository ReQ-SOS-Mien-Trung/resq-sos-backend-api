namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestStatusCounts;

public class GetSosRequestStatusCountsResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Total { get; set; }
    public List<SosRequestStatusCountDto> StatusCounts { get; set; } = [];
}

public class SosRequestStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}
