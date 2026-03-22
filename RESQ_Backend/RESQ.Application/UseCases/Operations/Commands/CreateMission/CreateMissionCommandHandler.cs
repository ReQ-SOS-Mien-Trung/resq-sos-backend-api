using MediatR;
using RESQ.Application.Extensions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandHandler(
    IMissionRepository missionRepository,
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    ILogger<CreateMissionCommandHandler> logger
) : IRequestHandler<CreateMissionCommand, CreateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IMediator _mediator = mediator;
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

        // Validate depot inventory for each activity that specifies supplies
        await ValidateSuppliesAsync(request.Activities, cancellationToken);

        // Build domain model
        var mission = new MissionModel
        {
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            PriorityScore = request.PriorityScore,
            Status = MissionStatus.Planned,
            StartTime = request.StartTime.ToUtcForStorage(),
            ExpectedEndTime = request.ExpectedEndTime.ToUtcForStorage(),
            IsCompleted = false,
            CreatedById = request.CreatedById,
            CreatedAt = DateTime.UtcNow,
            Activities = request.Activities.Select((a, idx) => new MissionActivityModel
            {
                Step = a.Step ?? (idx + 1),
                ActivityCode = a.ActivityCode,
                ActivityType = a.ActivityType,
                Description = a.Description,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                Items = a.SuppliesToCollect is { Count: > 0 }
                    ? JsonSerializer.Serialize(a.SuppliesToCollect.Select(s => new SupplyToCollectDto
                    {
                        ItemId = s.Id,
                        ItemName = s.Name ?? string.Empty,
                        Quantity = s.Quantity ?? 0,
                        Unit = s.Unit
                    }))
                    : null,
                Target = a.Target,
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = MissionActivityStatus.Planned
            }).ToList()
        };

        var missionId = await _missionRepository.CreateAsync(mission, request.CreatedById, cancellationToken);

        // Mark cluster as having a mission created
        cluster.IsMissionCreated = true;
        await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);

        // Update all SOS requests in cluster to Assigned
        await _sosRequestRepository.UpdateStatusByClusterIdAsync(request.ClusterId, SosRequestStatus.Assigned, cancellationToken);

        await _unitOfWork.SaveAsync();

        // Reserve supplies in inventory for all activities that specify depot + items
        await ReserveSuppliesAsync(request.Activities, cancellationToken);

        // Assign rescue teams per activity (using suggestedTeam.id / RescueTeamId)
        var savedActivities = await _missionRepository.GetByIdAsync(missionId, cancellationToken);
        if (savedActivities?.Activities is not null)
        {
            var activityList = savedActivities.Activities
                .OrderBy(a => a.Step)
                .ToList();

            var requestActivities = request.Activities
                .OrderBy(a => a.Step ?? int.MaxValue)
                .ToList();

            for (int i = 0; i < Math.Min(activityList.Count, requestActivities.Count); i++)
            {
                var act = requestActivities[i];
                if (!act.RescueTeamId.HasValue) continue;

                try
                {
                    var assignCmd = new AssignTeamToActivityCommand(
                        activityList[i].Id,
                        missionId,
                        act.RescueTeamId.Value,
                        request.CreatedById
                    );
                    await _mediator.Send(assignCmd, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not assign RescueTeamId={teamId} to ActivityId={actId} in MissionId={missionId}",
                        act.RescueTeamId.Value, activityList[i].Id, missionId);
                }
            }
        }

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

    private async Task ValidateSuppliesAsync(List<CreateActivityItemDto> activities, CancellationToken cancellationToken)
    {
        // Group activities by depotId — only validate those that specify both a depot and supplies
        var activitiesWithSupplies = activities
            .Where(a => a.DepotId.HasValue && a.SuppliesToCollect is { Count: > 0 })
            .ToList();

        // Aggregate items per depot (sum quantities if same item appears in multiple activities of same depot)
        var byDepot = activitiesWithSupplies
            .GroupBy(a => a.DepotId!.Value)
            .Select(g => new
            {
                DepotId = g.Key,
                Items = g.SelectMany(a => a.SuppliesToCollect!)
                         .Where(s => s.Id.HasValue)
                         .GroupBy(s => s.Id!.Value)
                         .Select(sg => (
                             ItemModelId: sg.Key,
                             ItemName: sg.First().Name ?? $"Item#{sg.Key}",
                             RequestedQuantity: sg.Sum(s => s.Quantity ?? 0)
                         ))
                         .ToList()
            });

        var allErrors = new List<string>();

        foreach (var depot in byDepot)
        {
            if (depot.Items.Count == 0) continue;

            var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(
                depot.DepotId, depot.Items, cancellationToken);

            foreach (var s in shortages)
            {
                allErrors.Add(s.NotFound
                    ? $"Kho {depot.DepotId}: Vật tư '{s.ItemName}' (ID={s.ItemModelId}) không có trong kho."
                    : $"Kho {depot.DepotId}: Vật tư '{s.ItemName}' (ID={s.ItemModelId}) không đủ số lượng — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
            }
        }

        if (allErrors.Count > 0)
            throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", allErrors)}");
    }

    private async Task ReserveSuppliesAsync(List<CreateActivityItemDto> activities, CancellationToken cancellationToken)
    {
        var activitiesWithSupplies = activities
            .Where(a => a.DepotId.HasValue && a.SuppliesToCollect is { Count: > 0 })
            .ToList();

        var byDepot = activitiesWithSupplies
            .GroupBy(a => a.DepotId!.Value)
            .Select(g => new
            {
                DepotId = g.Key,
                Items = g.SelectMany(a => a.SuppliesToCollect!)
                         .Where(s => s.Id.HasValue && (s.Quantity ?? 0) > 0)
                         .GroupBy(s => s.Id!.Value)
                         .Select(sg => (ItemModelId: sg.Key, Quantity: sg.Sum(s => s.Quantity ?? 0)))
                         .ToList()
            });

        foreach (var depot in byDepot)
        {
            if (depot.Items.Count == 0) continue;
            try
            {
                await _depotInventoryRepository.ReserveSuppliesAsync(depot.DepotId, depot.Items, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Không thể đặt trước vật tư tại kho {DepotId} khi tạo mission", depot.DepotId);
            }
        }
    }
}
