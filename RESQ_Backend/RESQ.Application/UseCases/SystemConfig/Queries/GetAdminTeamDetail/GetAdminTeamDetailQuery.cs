using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;

public record GetAdminTeamDetailQuery(int TeamId) : IRequest<AdminTeamDetailDto>;
