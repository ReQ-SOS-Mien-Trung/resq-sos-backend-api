namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllPrompts;

public class GetAllPromptsResponse
{
    public List<PromptDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
