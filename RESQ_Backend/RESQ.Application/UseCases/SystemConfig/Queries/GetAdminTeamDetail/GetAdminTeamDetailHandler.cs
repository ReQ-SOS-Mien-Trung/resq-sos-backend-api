using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;

public class GetAdminTeamDetailHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetAdminTeamDetailHandler> logger
) : IRequestHandler<GetAdminTeamDetailQuery, AdminTeamDetailDto>
{
    public async Task<AdminTeamDetailDto> Handle(
        GetAdminTeamDetailQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetAdminTeamDetail teamId={id}", request.TeamId);

        var dto = await dashboardRepository.GetAdminTeamDetailAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội với ID = {request.TeamId}");

        return dto;
    }
}
