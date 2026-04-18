using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetRoles;

public record GetRolesQuery : IRequest<List<GetRolesItemResponse>>;
