using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptById;

public record GetPromptByIdQuery(int Id) : IRequest<GetPromptByIdResponse>;
