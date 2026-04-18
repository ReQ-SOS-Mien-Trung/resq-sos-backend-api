using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetUserById;

public record GetUserByIdQuery(Guid UserId) : IRequest<GetUserByIdResponse>;
