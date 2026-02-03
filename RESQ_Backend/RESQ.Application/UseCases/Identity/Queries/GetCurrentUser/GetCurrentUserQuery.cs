using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public record GetCurrentUserQuery(Guid UserId) : IRequest<GetCurrentUserResponse>;
}
