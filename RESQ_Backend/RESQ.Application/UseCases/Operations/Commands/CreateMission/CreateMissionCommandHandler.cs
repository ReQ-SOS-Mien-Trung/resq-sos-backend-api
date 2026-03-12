using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandHandler(
    IMissionRepository missionRepository,
    ISosClusterRepository sosClusterRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateMissionCommandHandler> logger
) : IRequestHandler<CreateMissionCommand, CreateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateMissionCommandHandler> _logger = logger;

    public async Task<CreateMissionResponse> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating mission for ClusterId={clusterId}, CreatedBy={userId}",
            request.ClusterId, request.CreatedById);

        // Validate cluster exists
        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        // Build domain model
        var mission = new MissionModel
        {
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            PriorityScore = request.PriorityScore,
            Status = MissionStatus.Planned,
            StartTime = request.StartTime,
            ExpectedEndTime = request.ExpectedEndTime,
            IsCompleted = false,
            CreatedById = request.CreatedById,
            CreatedAt = DateTime.UtcNow,
            Activities = request.Activities.Select((a, idx) => new MissionActivityModel
            {
                Step = a.Step ?? (idx + 1),
                ActivityCode = a.ActivityCode,
                ActivityType = a.ActivityType,
                Description = a.Description,
                Target = a.Target,
                Items = a.Items,
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = MissionActivityStatus.Planned
            }).ToList()
        };

        var missionId = await _missionRepository.CreateAsync(mission, request.CreatedById, cancellationToken);

        // Mark cluster as having a mission created
        cluster.IsMissionCreated = true;
        await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);

        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Mission created: MissionId={missionId}", missionId);

        return new CreateMissionResponse
        {
            MissionId = missionId,
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            Status = "planned",
            ActivityCount = request.Activities.Count,
            CreatedAt = mission.CreatedAt
        };
    }
}
