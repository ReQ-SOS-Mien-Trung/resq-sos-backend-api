using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionCommandHandler(
    IMissionRepository missionRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionCommandHandler> logger
) : IRequestHandler<UpdateMissionCommand, UpdateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionCommandHandler> _logger = logger;

    public async Task<UpdateMissionResponse> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        mission.MissionType = request.MissionType;
        mission.PriorityScore = request.PriorityScore;
        mission.StartTime = request.StartTime;
        mission.ExpectedEndTime = request.ExpectedEndTime;

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateMissionResponse
        {
            MissionId = mission.Id,
            MissionType = mission.MissionType,
            PriorityScore = mission.PriorityScore,
            StartTime = mission.StartTime,
            ExpectedEndTime = mission.ExpectedEndTime
        };
    }
}
