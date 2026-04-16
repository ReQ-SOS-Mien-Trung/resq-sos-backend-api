using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;

public class GetRescuerMissionScoresHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetRescuerMissionScoresHandler> logger
) : IRequestHandler<GetRescuerMissionScoresQuery, RescuerMissionScoresDto>
{
    public async Task<RescuerMissionScoresDto> Handle(
        GetRescuerMissionScoresQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetRescuerMissionScores rescuerId={id}", request.RescuerId);

        var dto = await dashboardRepository.GetRescuerMissionScoresAsync(request.RescuerId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy rescuer với ID = {request.RescuerId}");

        return dto;
    }
}
