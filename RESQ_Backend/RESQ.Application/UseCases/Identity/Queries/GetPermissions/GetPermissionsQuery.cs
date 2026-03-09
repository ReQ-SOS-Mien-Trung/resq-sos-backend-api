using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetPermissions;

public record GetPermissionsQuery : IRequest<List<GetPermissionsItemResponse>>;
