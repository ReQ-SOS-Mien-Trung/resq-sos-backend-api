using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusCommandHandler(
    IMissionRepository missionRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionStatusCommandHandler> logger
) : IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionStatusCommandHandler> _logger = logger;

    private static readonly HashSet<string> ValidStatuses = ["pending", "in_progress", "completed", "cancelled"];

    public async Task<UpdateMissionStatusResponse> Handle(UpdateMissionStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for MissionId={missionId} -> {status}", request.MissionId, request.Status);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        bool isCompleted = request.Status == "completed";
        await _missionRepository.UpdateStatusAsync(request.MissionId, request.Status, isCompleted, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateMissionStatusResponse
        {
            MissionId = request.MissionId,
            Status = request.Status,
            IsCompleted = isCompleted
        };
    }
}
