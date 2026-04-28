using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;

public class GetAdminTeamListHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetAdminTeamListHandler> logger
) : IRequestHandler<GetAdminTeamListQuery, PagedResult<AdminTeamListItemDto>>
{
    public async Task<PagedResult<AdminTeamListItemDto>> Handle(
        GetAdminTeamListQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "GetAdminTeamList page={page} size={size} teamType={teamType} status={status} assemblyPointName={assemblyPointName} search={search}",
            request.PageNumber,
            request.PageSize,
            request.TeamType,
            request.Status,
            request.AssemblyPointName,
            request.Search);

        return await dashboardRepository.GetAdminTeamListAsync(
            request.PageNumber,
            request.PageSize,
            request.TeamType,
            request.Status,
            request.AssemblyPointName,
            request.Search,
            cancellationToken);
    }
}
