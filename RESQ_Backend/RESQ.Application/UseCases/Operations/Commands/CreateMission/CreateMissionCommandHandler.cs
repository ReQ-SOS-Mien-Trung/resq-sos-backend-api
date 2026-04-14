using MediatR;
using RESQ.Application.Extensions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Domain.Enum.Logistics;

public class CreateMissionCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IDepotInventoryRepository depotInventoryRepository, IDepotRepository depotRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IRescueTeamRepository rescueTeamRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    IFirebaseService firebaseService,
    ILogger<CreateMissionCommandHandler> logger
) : IRequestHandler<CreateMissionCommand, CreateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IMediator _mediator = mediator;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<CreateMissionCommandHandler> _logger = logger;

    private const string CollectSuppliesActivityType = "COLLECT_SUPPLIES";
    private const string ReturnSuppliesActivityType = "RETURN_SUPPLIES";
    private const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";
    private const string ReusableItemType = "Reusable";
    private const double DefaultBufferRatio = 0.10;
    public async Task<CreateMissionResponse> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating mission for ClusterId={clusterId}, CreatedBy={userId}",
            request.ClusterId, request.CreatedById);

        // Validate cluster exists
        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm th?y cluster v?i ID: {request.ClusterId}");

        // Validate assembly points
        var requestedApIds = (request.Activities ?? [])
            .Where(a => a.AssemblyPointId.HasValue)
            .Select(a => a.AssemblyPointId!.Value)
            .Distinct()
            .ToList();
        
        if (requestedApIds.Count > 0)
        {
            var assemblyPointErrors = new List<string>();

            foreach (var assemblyPointId in requestedApIds)
            {
                var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(assemblyPointId, cancellationToken);
                if (assemblyPoint is null)
                {
                    assemblyPointErrors.Add($"Khong tim thay diem tap ket #{assemblyPointId}.");
                    continue;
                }

                if (assemblyPoint.Status == AssemblyPointStatus.Unavailable
                    || assemblyPoint.Status == AssemblyPointStatus.Closed)
                {
                    assemblyPointErrors.Add($"Khong the dieu phoi den diem tap ket #{assemblyPointId} vi dang dong hoac khong kha dung.");
                }

                if (assemblyPoint.Location is null)
                {
                    assemblyPointErrors.Add($"Diem tap ket #{assemblyPointId} chua co toa do hop le.");
                }
            }

            if (assemblyPointErrors.Count > 0)
            {
                throw new BadRequestException($"Ke hoach mission chua hop le voi diem tap ket:\n{string.Join("\n", assemblyPointErrors)}");
            }
        }
        var activities = (request.Activities ?? [])
            .Select(CloneActivity)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ToList();

        await ValidateReturnAssemblyPointActivitiesAsync(activities, cancellationToken);

                // Validate that all depots used in activities are active
        var depotIds = activities
            .Where(a => a.DepotId.HasValue)
            .Select(a => a.DepotId!.Value)
            .Distinct();

        foreach (var id in depotIds)
        {
            var status = await _depotRepository.GetStatusByIdAsync(id, cancellationToken);
            if (status is null)
                throw new NotFoundException($"Không tìm th?y kho có ID {id}.");
                
            if (status is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            {
                throw new ConflictException($"Kho {id} dang ? tr?ng thái {status} và không th? s? d?ng cho nhi?m v?.");
            }
        }

        // Validate depot inventory for each activity that specifies supplies
        await ValidateSuppliesAsync(activities, cancellationToken);
        await ValidateReusableReturnActivitiesAsync(activities, cancellationToken);

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
            Activities = activities.Select((a, idx) => new MissionActivityModel
            {
                Step = a.Step ?? (idx + 1),
                ActivityType = a.ActivityType,
                Description = a.Description,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                AssemblyPointId = a.AssemblyPointId,
                Items = a.SuppliesToCollect is { Count: > 0 }
                    ? JsonSerializer.Serialize(a.SuppliesToCollect.Select(s =>
                    {
                        var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                        var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                        return new SupplyToCollectDto
                        {
                            ItemId = s.Id,
                            ItemName = s.Name ?? string.Empty,
                            Quantity = s.Quantity ?? 0,
                            Unit = s.Unit,
                            BufferRatio = bufferRatio > 0 ? bufferRatio : (double?)null,
                            BufferQuantity = bufferQty > 0 ? bufferQty : (int?)null
                        };
                    }))
                    : null,
                Target = a.Target,
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = MissionActivityStatus.Planned
            }).ToList()
        };

        var missionId = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            missionId = await _missionRepository.CreateAsync(mission, request.CreatedById, cancellationToken);

            // Mark cluster as having a mission created
            cluster.IsMissionCreated = true;
            await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);

            // Update all SOS requests in cluster to Assigned
            await _sosRequestRepository.UpdateStatusByClusterIdAsync(request.ClusterId, SosRequestStatus.Assigned, cancellationToken);

            await _unitOfWork.SaveAsync();

            // Assign rescue teams per activity (using suggestedTeam.id / RescueTeamId)
            var savedActivities = await _missionRepository.GetByIdAsync(missionId, cancellationToken);
            if (savedActivities?.Activities is not null)
            {
                var activityList = savedActivities.Activities
                    .OrderBy(a => a.Step)
                    .ToList();

                var requestActivities = activities
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

            savedActivities = await _missionRepository.GetByIdAsync(missionId, cancellationToken);
            if (savedActivities?.Activities is not null)
            {
                var persistedActivities = savedActivities.Activities
                    .OrderBy(a => a.Step ?? int.MaxValue)
                    .ThenBy(a => a.Id)
                    .ToList();
                var requestActivities = activities
                    .OrderBy(a => a.Step ?? int.MaxValue)
                    .ToList();

                await ReserveSuppliesAsync(requestActivities, persistedActivities, cancellationToken);
            }
        });

        _logger.LogInformation("Mission created: MissionId={missionId}", missionId);

        // Notify team members about the new mission assignment
        await NotifyTeamMembersAsync(activities, missionId, cancellationToken);

        return new CreateMissionResponse
        {
            MissionId = missionId,
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            Status = "planned",
            ActivityCount = activities.Count,
            CreatedAt = mission.CreatedAt
        };
    }

    private static CreateActivityItemDto CloneActivity(CreateActivityItemDto activity) => new()
    {
        Step = activity.Step,
        ActivityType = activity.ActivityType,
        Description = activity.Description,
        Priority = activity.Priority,
        EstimatedTime = activity.EstimatedTime,
        SosRequestId = activity.SosRequestId,
        DepotId = activity.DepotId,
        DepotName = activity.DepotName,
        DepotAddress = activity.DepotAddress,
        AssemblyPointId = activity.AssemblyPointId,
        SuppliesToCollect = activity.SuppliesToCollect?.Select(supply => new SuggestedSupplyItemDto
        {
            Id = supply.Id,
            Name = supply.Name,
            Quantity = supply.Quantity,
            Unit = supply.Unit,
            BufferRatio = supply.BufferRatio
        }).ToList(),
        Target = activity.Target,
        TargetLatitude = activity.TargetLatitude,
        TargetLongitude = activity.TargetLongitude,
        RescueTeamId = activity.RescueTeamId
    };

    private Task ValidateReturnAssemblyPointActivitiesAsync(
        List<CreateActivityItemDto> activities,
        CancellationToken cancellationToken)
    {
        var teamIds = activities
            .Where(activity => activity.RescueTeamId.HasValue)
            .Select(activity => activity.RescueTeamId!.Value)
            .Distinct()
            .ToList();

        if (teamIds.Count == 0)
            return Task.CompletedTask;

        var errors = new List<string>();
        var orderedActivities = activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ToList();

        var seenReturnAssemblyActivity = false;
        foreach (var activity in orderedActivities)
        {
            if (IsReturnAssemblyPointActivity(activity))
            {
                seenReturnAssemblyActivity = true;
                continue;
            }

            if (seenReturnAssemblyActivity)
            {
                errors.Add(
                    $"RETURN_ASSEMBLY_POINT phai nam o cuoi ke hoach, nhung phat hien activity '{activity.ActivityType}' sau buoc nay (step {activity.Step ?? 0}).");
            }
        }

        var returnActivities = activities
            .Where(IsReturnAssemblyPointActivity)
            .ToList();

        if (returnActivities.Count == 0)
            errors.Add("Mission phai co RETURN_ASSEMBLY_POINT o cuoi ke hoach.");

        foreach (var group in returnActivities.GroupBy(activity => activity.RescueTeamId))
        {
            if (!group.Key.HasValue)
            {
                var stepLabel = group.First().Step?.ToString() ?? "?";
                errors.Add($"RETURN_ASSEMBLY_POINT step {stepLabel} phai co RescueTeamId.");
                continue;
            }

            if (group.Count() > 1)
                errors.Add($"Mission chi duoc co mot RETURN_ASSEMBLY_POINT cho doi #{group.Key.Value}.");
        }

        var returnTeamIds = returnActivities
            .Where(activity => activity.RescueTeamId.HasValue)
            .Select(activity => activity.RescueTeamId!.Value)
            .ToHashSet();

        foreach (var teamId in teamIds)
        {
            if (!returnTeamIds.Contains(teamId))
                errors.Add($"Thieu RETURN_ASSEMBLY_POINT o cuoi ke hoach cho doi #{teamId}.");
        }

        foreach (var activity in returnActivities)
        {
            if (!activity.AssemblyPointId.HasValue || activity.AssemblyPointId.Value <= 0)
            {
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} phai co AssemblyPointId hop le.");
            }

            if (activity.DepotId.HasValue)
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} khong duoc co DepotId.");

            if (activity.SuppliesToCollect is { Count: > 0 })
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} khong duoc co SuppliesToCollect.");
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException($"Ke hoach mission chua hop le voi RETURN_ASSEMBLY_POINT:\n{string.Join("\n", errors)}");
        }

        return Task.CompletedTask;
    }

    private async Task ValidateSuppliesAsync(List<CreateActivityItemDto> activities, CancellationToken cancellationToken)
    {
        // Only COLLECT_SUPPLIES steps actually draw from depot inventory.
        // DELIVER_SUPPLIES (and others) may carry suppliesToCollect metadata but do NOT
        // represent a separate depot withdrawal - including them would double-count quantities.
        var activitiesWithSupplies = activities
            .Where(a => IsCollectSuppliesActivity(a)
                && a.DepotId.HasValue
                && a.SuppliesToCollect is { Count: > 0 })
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
                         .Select(sg =>
                         {
                             var first = sg.First();
                             var totalRequired = sg.Sum(s => s.Quantity ?? 0);
                             var bufferRatio = Math.Max(0.0, first.BufferRatio ?? DefaultBufferRatio);
                             var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling(totalRequired * bufferRatio) : 0;
                             return (
                                 ItemModelId: sg.Key,
                                 ItemName: first.Name ?? $"Item#{sg.Key}",
                                 RequestedQuantity: totalRequired + bufferQty
                             );
                         })
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
                    ? $"Kho {depot.DepotId}: v?t ph?m '{s.ItemName}' (ID={s.ItemModelId}) không có trong kho."
                    : $"Kho {depot.DepotId}: v?t ph?m '{s.ItemName}' (ID={s.ItemModelId}) không d? s? lu?ng — yêu c?u {s.RequestedQuantity}, kh? d?ng {s.AvailableQuantity}.");
            }
        }

        if (allErrors.Count > 0)
            throw new BadRequestException($"Ki?m tra t?n kho th?t b?i:\n{string.Join("\n", allErrors)}");
    }

    private async Task ValidateReusableReturnActivitiesAsync(
        List<CreateActivityItemDto> activities,
        CancellationToken cancellationToken)
    {
        var orderedActivities = activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ToList();
        var returnActivities = activities
            .Where(IsReturnSuppliesActivity)
            .ToList();

        var seenReturnActivity = false;
        var orderingErrors = new List<string>();
        foreach (var activity in orderedActivities)
        {
            if (IsReturnSuppliesActivity(activity))
            {
                seenReturnActivity = true;
                continue;
            }

            if (seenReturnActivity && !IsReturnAssemblyPointActivity(activity))
            {
                orderingErrors.Add(
                    $"RETURN_SUPPLIES ph?i n?m ? cu?i k? ho?ch, nhung phát hi?n activity '{activity.ActivityType}' sau bu?c tr? kho (step {activity.Step ?? 0}).");
            }
        }

        var allSupplies = activities
            .SelectMany(activity => activity.SuppliesToCollect ?? [])
            .Where(supply => supply.Id.HasValue)
            .Select(supply => supply.Id!.Value)
            .Distinct()
            .ToList();

        if (allSupplies.Count == 0)
        {
            foreach (var returnActivity in returnActivities)
            {
                orderingErrors.Add(
                    $"RETURN_SUPPLIES step {returnActivity.Step ?? 0} ph?i có item_id h?p l? cho v?t ph?m reusable c?n tr?.");
            }

            if (orderingErrors.Count > 0)
                throw new BadRequestException($"K? ho?ch mission chua h?p l?:\n{string.Join("\n", orderingErrors)}");

            return;
        }

        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(allSupplies, cancellationToken);
        var missingItemIds = allSupplies
            .Where(itemId => !itemLookup.ContainsKey(itemId))
            .ToList();

        if (missingItemIds.Count > 0)
        {
            throw new BadRequestException(
                $"Không tìm th?y metadata cho các item_id: {string.Join(", ", missingItemIds)}.");
        }

        var errors = new List<string>(orderingErrors);
        var requiredReturnItems = new Dictionary<(int DepotId, int TeamId), Dictionary<int, int>>();
        var actualReturnItems = new Dictionary<(int DepotId, int TeamId), Dictionary<int, int>>();

        foreach (var activity in activities)
        {
            if (!IsCollectSuppliesActivity(activity)
                || !activity.DepotId.HasValue
                || !activity.RescueTeamId.HasValue
                || activity.SuppliesToCollect is not { Count: > 0 })
            {
                continue;
            }

            var key = (activity.DepotId.Value, activity.RescueTeamId.Value);
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (!supply.Id.HasValue || (supply.Quantity ?? 0) <= 0)
                    continue;

                if (!IsReusableItem(supply.Id.Value, itemLookup))
                    continue;

                var expectedItems = GetOrCreateItemBucket(requiredReturnItems, key);
                expectedItems[supply.Id.Value] = expectedItems.GetValueOrDefault(supply.Id.Value) + supply.Quantity!.Value;
            }
        }

        foreach (var activity in returnActivities)
        {
            var stepLabel = activity.Step?.ToString() ?? "?";
            if (!activity.DepotId.HasValue)
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thi?u DepotId.");
                continue;
            }

            if (!activity.RescueTeamId.HasValue)
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thi?u RescueTeamId.");
                continue;
            }

            if (activity.SuppliesToCollect is not { Count: > 0 })
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} ph?i có danh sách v?t ph?m reusable c?n tr?.");
                continue;
            }

            var key = (activity.DepotId.Value, activity.RescueTeamId.Value);
            var actualItems = GetOrCreateItemBucket(actualReturnItems, key);
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (!supply.Id.HasValue || (supply.Quantity ?? 0) <= 0)
                {
                    errors.Add($"RETURN_SUPPLIES step {stepLabel} có v?t ph?m thi?u item_id ho?c quantity không h?p l?.");
                    continue;
                }

                if (!IsReusableItem(supply.Id.Value, itemLookup))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES step {stepLabel} ch? du?c ch?a v?t ph?m reusable, nhung item '{ResolveItemName(supply.Id.Value, itemLookup, supply.Name)}' không ph?i reusable.");
                    continue;
                }

                actualItems[supply.Id.Value] = actualItems.GetValueOrDefault(supply.Id.Value) + supply.Quantity!.Value;
            }
        }

        foreach (var actualGroup in actualReturnItems)
        {
            if (!requiredReturnItems.TryGetValue(actualGroup.Key, out var expectedItems))
            {
                errors.Add(
                    $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, d?i {actualGroup.Key.TeamId} không tuong ?ng v?i b?t k? COLLECT_SUPPLIES nào có v?t ph?m reusable.");
                continue;
            }

            foreach (var actualItem in actualGroup.Value)
            {
                if (!expectedItems.TryGetValue(actualItem.Key, out var expectedQuantity))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, d?i {actualGroup.Key.TeamId} dang tr? thêm item '{ResolveItemName(actualItem.Key, itemLookup)}' không xu?t hi?n trong COLLECT_SUPPLIES reusable tuong ?ng.");
                    continue;
                }

                if (actualItem.Value != expectedQuantity)
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, d?i {actualGroup.Key.TeamId} ph?i tr? dúng {expectedQuantity} don v? item '{ResolveItemName(actualItem.Key, itemLookup)}', nhung payload hi?n có {actualItem.Value}.");
                }
            }
        }

        foreach (var expectedGroup in requiredReturnItems)
        {
            if (!actualReturnItems.TryGetValue(expectedGroup.Key, out var actualItems))
            {
                errors.Add(
                    $"Thi?u RETURN_SUPPLIES cu?i k? ho?ch cho kho {expectedGroup.Key.DepotId}, d?i {expectedGroup.Key.TeamId} dù dã COLLECT_SUPPLIES v?t ph?m reusable.");
                continue;
            }

            foreach (var expectedItem in expectedGroup.Value)
            {
                if (!actualItems.ContainsKey(expectedItem.Key))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {expectedGroup.Key.DepotId}, d?i {expectedGroup.Key.TeamId} chua tr? item '{ResolveItemName(expectedItem.Key, itemLookup)}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException($"K? ho?ch mission chua h?p l? v?i v?t ph?m reusable:\n{string.Join("\n", errors)}");
        }
    }

    private async Task NotifyTeamMembersAsync(List<CreateActivityItemDto> activities, int missionId, CancellationToken cancellationToken)
    {
        var teamIds = activities
            .Where(a => a.RescueTeamId.HasValue)
            .Select(a => a.RescueTeamId!.Value)
            .Distinct()
            .ToList();

        foreach (var teamId in teamIds)
        {
            try
            {
                var team = await _rescueTeamRepository.GetByIdAsync(teamId, cancellationToken);
                if (team?.Members is null) continue;

                foreach (var member in team.Members)
                {
                    try
                    {
                        await _firebaseService.SendNotificationToUserAsync(
                            member.UserId,
                            "Nhi?m v? m?i",
                            $"Ð?i c?a b?n dã du?c phân công vào nhi?m v? #{missionId}. Vui lòng ki?m tra chi ti?t.",
                            "mission_assigned",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to send notification to UserId={userId} for MissionId={missionId}",
                            member.UserId, missionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch team or notify members for RescueTeamId={teamId}, MissionId={missionId}",
                    teamId, missionId);
            }
        }
    }

    private async Task ReserveSuppliesAsync(
        List<CreateActivityItemDto> requestActivities,
        List<MissionActivityModel> persistedActivities,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < Math.Min(requestActivities.Count, persistedActivities.Count); index++)
        {
            var requestActivity = requestActivities[index];
            var persistedActivity = persistedActivities[index];

            if (!IsCollectSuppliesActivity(requestActivity)
                || !requestActivity.DepotId.HasValue
                || requestActivity.SuppliesToCollect is not { Count: > 0 })
            {
                continue;
            }

            var itemsToReserve = requestActivity.SuppliesToCollect
                .Where(s => s.Id.HasValue && (s.Quantity ?? 0) > 0)
                .Select(s =>
                {
                    var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                    var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                    return (ItemModelId: s.Id!.Value, Quantity: (s.Quantity ?? 0) + bufferQty);
                })
                .ToList();

            if (itemsToReserve.Count == 0)
                continue;

            try
            {
                var reservationResult = await _depotInventoryRepository.ReserveSuppliesAsync(
                    requestActivity.DepotId.Value,
                    itemsToReserve,
                    cancellationToken);

                await MissionSupplyExecutionSnapshotHelper.SyncReservationSnapshotAsync(
                    persistedActivity,
                    reservationResult,
                    _missionActivityRepository,
                    _logger,
                    cancellationToken);
                await _unitOfWork.SaveAsync();
            }
            catch (InvalidOperationException ex)
            {
                var activityLabel = persistedActivity.Step.HasValue
                    ? $"bu?c {persistedActivity.Step.Value}"
                    : $"ho?t d?ng #{persistedActivity.Id}";

                throw new BadRequestException(
                    $"Không th? t?o mission vì kho #{requestActivity.DepotId.Value} không d?t tru?c du?c v?t ph?m cho {activityLabel}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Không th? d?t tru?c v?t ph?m t?i kho {DepotId} cho activity #{ActivityId} khi t?o mission",
                    requestActivity.DepotId.Value,
                    persistedActivity.Id);
                throw;
            }
        }
    }

    private static bool IsCollectSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnAssemblyPointActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReusableItem(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup) =>
        itemLookup.TryGetValue(itemId, out var item)
        && string.Equals(item.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<int, int> GetOrCreateItemBucket(
        IDictionary<(int DepotId, int TeamId), Dictionary<int, int>> buckets,
        (int DepotId, int TeamId) key)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = [];
            buckets[key] = bucket;
        }

        return bucket;
    }

    private static string ResolveItemName(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup,
        string? fallbackName = null)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName;

        return itemLookup.TryGetValue(itemId, out var item)
            ? item.Name
            : $"Item#{itemId}";
    }
}


