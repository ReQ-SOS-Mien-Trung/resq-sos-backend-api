using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.AiConfigs;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigVersions;

public record GetAiConfigVersionsQuery(int Id) : IRequest<GetAiConfigVersionsResponse>;

public class GetAiConfigVersionsResponse
{
    public int SourceAiConfigId { get; set; }
    public List<AiConfigSummaryDto> Items { get; set; } = [];
}
