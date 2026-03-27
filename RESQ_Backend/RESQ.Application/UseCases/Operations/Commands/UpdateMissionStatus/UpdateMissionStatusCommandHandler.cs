using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusCommandHandler(
    IMissionRepository missionRepository,
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionStatusCommandHandler> logger
) : IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionStatusCommandHandler> _logger = logger;

    public async Task<UpdateMissionStatusResponse> Handle(UpdateMissionStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for MissionId={missionId} -> {status}", request.MissionId, request.Status);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        if (request.Status is MissionStatus.Completed or MissionStatus.Incompleted)
            throw new BadRequestException("Mission chỉ được chốt sau khi các đội cứu hộ nộp báo cáo cuối cùng.");

        MissionStateMachine.EnsureValidTransition(mission.Status, request.Status);

        bool isCompleted = request.Status == MissionStatus.Completed || request.Status == MissionStatus.Incompleted;
        await _missionRepository.UpdateStatusAsync(request.MissionId, request.Status, isCompleted, cancellationToken);

        // Side-effects: update SOS requests in the cluster
        if (mission.ClusterId.HasValue)
        {
            if (request.Status == MissionStatus.OnGoing)
            {
                await _sosRequestRepository.UpdateStatusByClusterIdAsync(mission.ClusterId.Value, SosRequestStatus.InProgress, cancellationToken);
            }
        }

        await _unitOfWork.SaveAsync();

        return new UpdateMissionStatusResponse
        {
            MissionId = request.MissionId,
            Status = request.Status.ToString(),
            IsCompleted = isCompleted
        };
    }
}
