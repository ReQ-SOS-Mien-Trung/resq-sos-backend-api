using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptVersions;

public record GetPromptVersionsQuery(int Id) : IRequest<GetPromptVersionsResponse>;
