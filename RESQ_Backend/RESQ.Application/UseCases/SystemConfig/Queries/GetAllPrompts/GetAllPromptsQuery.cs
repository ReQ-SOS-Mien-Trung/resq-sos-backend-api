using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllPrompts;

public class GetAllPromptsQuery : IRequest<GetAllPromptsResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
