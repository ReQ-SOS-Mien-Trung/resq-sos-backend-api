using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllAiConfigs;

public class GetAllAiConfigsQuery : IRequest<GetAllAiConfigsResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
