using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionCommandHandler(
    IMissionRepository missionRepository,
    IUnitOfWork unitOfWork,
    IMissionPendingActivityUpdateService missionPendingActivityUpdateService,
    IAdminRealtimeHubService adminRealtimeHubService,
    ISender sender,
    ILogger<UpdateMissionCommandHandler> logger
) : IRequestHandler<UpdateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IMissionPendingActivityUpdateService _missionPendingActivityUpdateService = missionPendingActivityUpdateService;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ISender _sender = sender;
    private readonly ILogger<UpdateMissionCommandHandler> _logger = logger;

    public async Task<MissionDto> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating MissionId={MissionId} with {ActivityCount} pending activity patch(es)",
            request.MissionId,
            request.Activities.Count);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y mission vá»›i ID: {request.MissionId}");

        mission.MissionType = request.MissionType;
        mission.PriorityScore = request.PriorityScore;
        mission.StartTime = request.StartTime.ToUtcForStorage();
        mission.ExpectedEndTime = request.ExpectedEndTime.ToUtcForStorage();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (request.Activities.Count > 0)
            {
                await _missionPendingActivityUpdateService.ApplyAsync(
                    mission,
                    request.UpdatedBy!.Value,
                    request.Activities,
                    cancellationToken);
            }

            await _missionRepository.UpdateAsync(mission, cancellationToken);
            await _unitOfWork.SaveAsync();
        });

        var result = await _sender.Send(new GetMissionByIdQuery(request.MissionId), cancellationToken);
        await _adminRealtimeHubService.PushMissionUpdateAsync(
            new AdminMissionRealtimeUpdate
            {
                EntityId = mission.Id,
                EntityType = "Mission",
                MissionId = mission.Id,
                ClusterId = mission.ClusterId,
                Action = "Updated",
                Status = mission.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        return result ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y mission vá»›i ID: {request.MissionId}");
    }
}
