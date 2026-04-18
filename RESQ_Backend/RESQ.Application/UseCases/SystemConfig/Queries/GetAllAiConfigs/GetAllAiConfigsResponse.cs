using RESQ.Application.UseCases.SystemConfig.Queries.AiConfigs;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllAiConfigs;

public class GetAllAiConfigsResponse
{
    public List<AiConfigSummaryDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
