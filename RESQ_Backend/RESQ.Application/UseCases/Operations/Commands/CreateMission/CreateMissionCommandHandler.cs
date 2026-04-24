using MediatR;
using RESQ.Application.Extensions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
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
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ISosAiAnalysisRepository sosAiAnalysisRepository,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    IDepotInventoryRepository depotInventoryRepository, IDepotRepository depotRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IRescueTeamRepository rescueTeamRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    IAdminRealtimeHubService adminRealtimeHubService,
    IFirebaseService firebaseService,
    ILogger<CreateMissionCommandHandler> logger
) : IRequestHandler<CreateMissionCommand, CreateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ISosAiAnalysisRepository _sosAiAnalysisRepository = sosAiAnalysisRepository;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IMediator _mediator = mediator;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
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
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        if (request.AiSuggestionId.HasValue)
        {
            var suggestion = await _missionAiSuggestionRepository.GetByIdAsync(request.AiSuggestionId.Value, cancellationToken);
            if (suggestion is null)
                throw new NotFoundException($"Không tìm thấy AI mission suggestion #{request.AiSuggestionId.Value}.");

            if (suggestion.ClusterId != request.ClusterId)
            {
                throw new BadRequestException(
                    $"AI mission suggestion #{request.AiSuggestionId.Value} không thuộc cluster #{request.ClusterId}.");
            }
        }

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
                    assemblyPointErrors.Add($"Không tìm thấy điểm tập kết #{assemblyPointId}.");
                    continue;
                }

                if (assemblyPoint.Status == AssemblyPointStatus.Unavailable
                    || assemblyPoint.Status == AssemblyPointStatus.Closed)
                {
                    assemblyPointErrors.Add($"Không thể điều phối đến điểm tập kết #{assemblyPointId} vì đang đóng hoặc không khả dụng.");
                }

                if (assemblyPoint.Location is null)
                {
                    assemblyPointErrors.Add($"Điểm tập kết #{assemblyPointId} chưa có tọa độ hợp lệ.");
                }
            }

            if (assemblyPointErrors.Count > 0)
            {
                throw new BadRequestException($"Kế hoạch mission chưa hợp lệ với điểm tập kết:\n{string.Join("\n", assemblyPointErrors)}");
            }
        }
        var activities = (request.Activities ?? [])
            .Select(CloneActivity)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ToList();
        await EnrichVictimDescriptionsAsync(activities, cancellationToken);

        var itemLookup = await LoadReferencedItemMetadataAsync(activities, cancellationToken);

        var mixedMissionIgnored = await ValidateMixedRescueReliefOverrideAsync(
            request.ClusterId,
            activities,
            request.IgnoreMixedMissionWarning,
            cancellationToken);

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
                throw new NotFoundException($"Không tìm thấy kho có ID {id}.");
                
            if (status is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            {
                throw new ConflictException($"Kho {id} đang ở trạng thái {status} và không thể sử dụng cho nhiệm vụ.");
            }
        }

        // Validate depot inventory for each activity that specifies supplies
        await ValidateSuppliesAsync(activities, itemLookup, cancellationToken);
        await ValidateReusableReturnActivitiesAsync(activities, itemLookup);

        var manualOverrideMetadata = mixedMissionIgnored
            ? MissionManualOverrideJsonHelper.Serialize(new MissionManualOverrideInfo
            {
                IgnoreMixedMissionWarning = true,
                OverrideReason = string.IsNullOrWhiteSpace(request.OverrideReason)
                    ? null
                    : request.OverrideReason.Trim(),
                OverriddenBy = request.CreatedById,
                OverriddenAt = DateTime.UtcNow
            })
            : null;

        // Build domain model
        var mission = new MissionModel
        {
            ClusterId = request.ClusterId,
            AiSuggestionId = request.AiSuggestionId,
            MissionType = request.MissionType,
            PriorityScore = request.PriorityScore,
            Status = MissionStatus.Planned,
            StartTime = request.StartTime.ToUtcForStorage(),
            ExpectedEndTime = request.ExpectedEndTime.ToUtcForStorage(),
            IsCompleted = false,
            CreatedById = request.CreatedById,
            CreatedAt = DateTime.UtcNow,
            ManualOverrideMetadata = manualOverrideMetadata,
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
                        var isBufferExempt = s.Id.HasValue && IsBufferExemptItem(s.Id.Value, itemLookup);
                        var bufferRatio = isBufferExempt ? 0.0 : Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
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

            cluster.Status = SosClusterStatus.InProgress;
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

                await ReserveSuppliesAsync(requestActivities, persistedActivities, itemLookup, cancellationToken);
            }
        });

        _logger.LogInformation("Mission created: MissionId={missionId}", missionId);

        // Notify team members about the new mission assignment
        await NotifyTeamMembersAsync(activities, missionId, cancellationToken);
        await _adminRealtimeHubService.PushMissionUpdateAsync(
            new AdminMissionRealtimeUpdate
            {
                EntityId = missionId,
                EntityType = "Mission",
                MissionId = missionId,
                ClusterId = request.ClusterId,
                Action = "Created",
                Status = MissionStatus.Planned.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
        await _adminRealtimeHubService.PushSOSClusterUpdateAsync(
            new AdminSOSClusterRealtimeUpdate
            {
                EntityId = request.ClusterId,
                EntityType = "SOSCluster",
                ClusterId = request.ClusterId,
                Action = "MissionCreated",
                Status = SosClusterStatus.InProgress.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

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

    private async Task EnrichVictimDescriptionsAsync(
        IEnumerable<CreateActivityItemDto> activities,
        CancellationToken cancellationToken)
    {
        var victimContexts = await MissionActivityVictimContextLoader.LoadAsync(
            activities
                .Where(activity => activity.SosRequestId.HasValue)
                .Select(activity => activity.SosRequestId!.Value),
            _sosRequestRepository,
            _sosRequestUpdateRepository,
            cancellationToken);

        foreach (var activity in activities)
        {
            if (!activity.SosRequestId.HasValue
                || !victimContexts.TryGetValue(activity.SosRequestId.Value, out var victimContext))
            {
                continue;
            }

            activity.Description = MissionActivityVictimContextHelper.ApplySummaryToDescription(
                activity.ActivityType,
                activity.Description,
                victimContext.Summary);
        }
    }

    private async Task<IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord>> LoadReferencedItemMetadataAsync(
        List<CreateActivityItemDto> activities,
        CancellationToken cancellationToken)
    {
        var itemIds = activities
            .SelectMany(activity => activity.SuppliesToCollect ?? [])
            .Where(supply => supply.Id.HasValue)
            .Select(supply => supply.Id!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count == 0)
            return new Dictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord>();

        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);
        var missingItemIds = itemIds
            .Where(itemId => !itemLookup.ContainsKey(itemId))
            .ToList();

        if (missingItemIds.Count > 0)
        {
            throw new BadRequestException(
                $"Không tìm thấy metadata cho các item_id: {string.Join(", ", missingItemIds)}.");
        }

        return itemLookup;
    }

    private async Task<bool> ValidateMixedRescueReliefOverrideAsync(
        int clusterId,
        List<CreateActivityItemDto> activities,
        bool ignoreMixedMissionWarning,
        CancellationToken cancellationToken)
    {
        var hasRescueBranch = activities.Any(IsRescueReliefActivity);
        var hasSupplyBranch = activities.Any(IsSupplyDistributionActivity);

        if (!hasRescueBranch || !hasSupplyBranch)
            return false;

        var clusterSosRequests = (await _sosRequestRepository.GetByClusterIdAsync(clusterId, cancellationToken)).ToList();
        var rescueActivitySosIds = activities
            .Where(IsRescueReliefActivity)
            .Where(activity => activity.SosRequestId.HasValue)
            .Select(activity => activity.SosRequestId!.Value)
            .ToHashSet();
        var reliefActivitySosIds = activities
            .Where(IsSupplyDistributionActivity)
            .Where(activity => activity.SosRequestId.HasValue)
            .Select(activity => activity.SosRequestId!.Value)
            .ToHashSet();

        var relevantRescueSos = clusterSosRequests
            .Where(sos =>
                rescueActivitySosIds.Count > 0
                    ? rescueActivitySosIds.Contains(sos.Id)
                    : SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType))
            .ToList();
        var relevantReliefSos = clusterSosRequests
            .Where(sos =>
                reliefActivitySosIds.Count > 0
                    ? reliefActivitySosIds.Contains(sos.Id)
                    : SosRequestAiAnalysisHelper.IsReliefRequestType(sos.SosType))
            .ToList();

        if (relevantRescueSos.Count == 0 || relevantReliefSos.Count == 0)
        {
            if (!ignoreMixedMissionWarning)
            {
                throw new BadRequestException(
                    "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát nhưng backend chưa xác định rõ SOS rescue và SOS relief trong cluster. " +
                    "Vui lòng tách mission hoặc gửi IgnoreMixedMissionWarning=true nếu coordinator chủ động chấp nhận rủi ro.");
            }

            return true;
        }

        var analysisLookup = await _sosAiAnalysisRepository.GetLatestBySosRequestIdsAsync(
            relevantRescueSos.Select(sos => sos.Id),
            cancellationToken);
        var missingAnalysisIds = relevantRescueSos
            .Where(sos => !analysisLookup.ContainsKey(sos.Id))
            .Select(sos => sos.Id)
            .OrderBy(id => id)
            .ToList();
        var urgentRescueIds = relevantRescueSos
            .Where(sos =>
            {
                analysisLookup.TryGetValue(sos.Id, out var analysis);
                var summary = SosRequestAiAnalysisHelper.FromAnalysis(analysis);
                return SosRequestAiAnalysisHelper.HasUrgentMixedMissionConstraint(
                    summary,
                    sos.PriorityLevel?.ToString());
            })
            .Select(sos => sos.Id)
            .OrderBy(id => id)
            .ToList();

        if (urgentRescueIds.Count == 0 && missingAnalysisIds.Count == 0)
            return false;

        if (!ignoreMixedMissionWarning)
        {
            throw new BadRequestException(BuildMixedMissionOverrideMessage(
                urgentRescueIds,
                relevantReliefSos.Select(sos => sos.Id).OrderBy(id => id).ToList(),
                missingAnalysisIds));
        }

        return true;
    }

    private static string BuildMixedMissionOverrideMessage(
        IReadOnlyCollection<int> urgentRescueIds,
        IReadOnlyCollection<int> reliefSosIds,
        IReadOnlyCollection<int> missingAnalysisIds)
    {
        var parts = new List<string>
        {
            "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát."
        };

        if (urgentRescueIds.Count > 0)
        {
            parts.Add(
                $"SOS rescue khẩn cấp cần ưu tiên tách riêng: {FormatSosIds(urgentRescueIds)}.");
        }

        if (reliefSosIds.Count > 0)
        {
            parts.Add(
                $"Đang bị ghép chung với nhánh cứu trợ/cấp phát của {FormatSosIds(reliefSosIds)}.");
        }

        if (missingAnalysisIds.Count > 0)
        {
            parts.Add(
                $"Chưa có SOS AI analysis để xác nhận khả năng chờ ghép mission cho {FormatSosIds(missingAnalysisIds)}.");
        }

        parts.Add(
            "Nếu coordinator vẫn muốn tiếp tục, hãy gửi IgnoreMixedMissionWarning=true.");

        return string.Join(" ", parts);
    }

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
                    $"RETURN_ASSEMBLY_POINT phải nằm ở cuối kế hoạch, nhưng phát hiện activity '{activity.ActivityType}' sau bước này (step {activity.Step ?? 0}).");
            }
        }

        var returnActivities = activities
            .Where(IsReturnAssemblyPointActivity)
            .ToList();

        if (returnActivities.Count == 0)
            errors.Add("Mission phải có RETURN_ASSEMBLY_POINT ở cuối kế hoạch.");

        foreach (var group in returnActivities.GroupBy(activity => activity.RescueTeamId))
        {
            if (!group.Key.HasValue)
            {
                var stepLabel = group.First().Step?.ToString() ?? "?";
                errors.Add($"RETURN_ASSEMBLY_POINT step {stepLabel} phải có RescueTeamId.");
                continue;
            }

            if (group.Count() > 1)
                errors.Add($"Mission chỉ được có một RETURN_ASSEMBLY_POINT cho đội #{group.Key.Value}.");
        }

        var returnTeamIds = returnActivities
            .Where(activity => activity.RescueTeamId.HasValue)
            .Select(activity => activity.RescueTeamId!.Value)
            .ToHashSet();

        foreach (var teamId in teamIds)
        {
            if (!returnTeamIds.Contains(teamId))
                errors.Add($"Thiếu RETURN_ASSEMBLY_POINT ở cuối kế hoạch cho đội #{teamId}.");
        }

        foreach (var activity in returnActivities)
        {
            if (!activity.AssemblyPointId.HasValue || activity.AssemblyPointId.Value <= 0)
            {
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} phải có AssemblyPointId hợp lệ.");
            }

            if (activity.DepotId.HasValue)
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} không được có DepotId.");

            if (activity.SuppliesToCollect is { Count: > 0 })
                errors.Add($"RETURN_ASSEMBLY_POINT step {activity.Step ?? 0} không được có SuppliesToCollect.");
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException($"Kế hoạch mission chưa hợp lệ với RETURN_ASSEMBLY_POINT:\n{string.Join("\n", errors)}");
        }

        return Task.CompletedTask;
    }

    private async Task ValidateSuppliesAsync(
        List<CreateActivityItemDto> activities,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup,
        CancellationToken cancellationToken)
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
                             var isBufferExempt = IsBufferExemptItem(sg.Key, itemLookup);
                             var bufferRatio = isBufferExempt ? 0.0 : Math.Max(0.0, first.BufferRatio ?? DefaultBufferRatio);
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
                    ? $"Kho {depot.DepotId}: vật phẩm '{s.ItemName}' (ID={s.ItemModelId}) không có trong kho."
                    : $"Kho {depot.DepotId}: vật phẩm '{s.ItemName}' (ID={s.ItemModelId}) không đủ số lượng — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
            }
        }

        if (allErrors.Count > 0)
            throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", allErrors)}");
    }

    private Task ValidateReusableReturnActivitiesAsync(
        List<CreateActivityItemDto> activities,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup)
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
                    $"RETURN_SUPPLIES phải nằm ở cuối kế hoạch, nhưng phát hiện activity '{activity.ActivityType}' sau bước trả kho (step {activity.Step ?? 0}).");
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
                    $"RETURN_SUPPLIES step {returnActivity.Step ?? 0} phải có item_id hợp lệ cho vật phẩm reusable cần trả.");
            }

            if (orderingErrors.Count > 0)
                throw new BadRequestException($"Kế hoạch mission chưa hợp lệ:\n{string.Join("\n", orderingErrors)}");

            return Task.CompletedTask;
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
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thiếu DepotId.");
                continue;
            }

            if (!activity.RescueTeamId.HasValue)
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thiếu RescueTeamId.");
                continue;
            }

            if (activity.SuppliesToCollect is not { Count: > 0 })
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} phải có danh sách vật phẩm reusable cần trả.");
                continue;
            }

            var key = (activity.DepotId.Value, activity.RescueTeamId.Value);
            var actualItems = GetOrCreateItemBucket(actualReturnItems, key);
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (!supply.Id.HasValue || (supply.Quantity ?? 0) <= 0)
                {
                    errors.Add($"RETURN_SUPPLIES step {stepLabel} có vật phẩm thiếu item_id hoặc quantity không hợp lệ.");
                    continue;
                }

                if (!IsReusableItem(supply.Id.Value, itemLookup))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES step {stepLabel} chỉ được chứa vật phẩm reusable, nhưng item '{ResolveItemName(supply.Id.Value, itemLookup, supply.Name)}' không phải reusable.");
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
                    $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} không tương ứng với bất kỳ COLLECT_SUPPLIES nào có vật phẩm reusable.");
                continue;
            }

            foreach (var actualItem in actualGroup.Value)
            {
                if (!expectedItems.TryGetValue(actualItem.Key, out var expectedQuantity))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} đang trả thêm item '{ResolveItemName(actualItem.Key, itemLookup)}' không xuất hiện trong COLLECT_SUPPLIES reusable tương ứng.");
                    continue;
                }

                if (actualItem.Value != expectedQuantity)
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} phải trả đúng {expectedQuantity} đơn vị item '{ResolveItemName(actualItem.Key, itemLookup)}', nhưng payload hiện có {actualItem.Value}.");
                }
            }
        }

        foreach (var expectedGroup in requiredReturnItems)
        {
            if (!actualReturnItems.TryGetValue(expectedGroup.Key, out var actualItems))
            {
                errors.Add(
                    $"Thiếu RETURN_SUPPLIES cuối kế hoạch cho kho {expectedGroup.Key.DepotId}, đội {expectedGroup.Key.TeamId} dù đã COLLECT_SUPPLIES vật phẩm reusable.");
                continue;
            }

            foreach (var expectedItem in expectedGroup.Value)
            {
                if (!actualItems.ContainsKey(expectedItem.Key))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {expectedGroup.Key.DepotId}, đội {expectedGroup.Key.TeamId} chưa trả item '{ResolveItemName(expectedItem.Key, itemLookup)}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException($"Kế hoạch mission chưa hợp lệ với vật phẩm reusable:\n{string.Join("\n", errors)}");
        }

        return Task.CompletedTask;
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
                            "Nhiệm vụ mới",
                            $"Đội của bạn đã được phân công vào nhiệm vụ #{missionId}. Vui lòng kiểm tra chi tiết.",
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
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup,
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
                    var isBufferExempt = IsBufferExemptItem(s.Id!.Value, itemLookup);
                    var bufferRatio = isBufferExempt ? 0.0 : Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
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
                    ? $"bước {persistedActivity.Step.Value}"
                    : $"hoạt động #{persistedActivity.Id}";

                throw new BadRequestException(
                    $"Không thể tạo mission vì kho #{requestActivity.DepotId.Value} không đặt trước được vật phẩm cho {activityLabel}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Không thể đặt trước vật phẩm tại kho {DepotId} cho activity #{ActivityId} khi tạo mission",
                    requestActivity.DepotId.Value,
                    persistedActivity.Id);
                throw;
            }
        }
    }

    private static bool IsCollectSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupplyDistributionActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static bool IsRescueReliefActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnAssemblyPointActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReusableItem(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup) =>
        itemLookup.TryGetValue(itemId, out var item)
        && string.Equals(item.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase);

    private static bool IsBufferExemptItem(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup) =>
        itemLookup.TryGetValue(itemId, out var item)
        && (string.Equals(item.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase)
            || item.CategoryId == (int)ItemCategoryCode.Vehicle);

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

    private static string FormatSosIds(IEnumerable<int> sosIds)
    {
        var ids = sosIds
            .Distinct()
            .OrderBy(id => id)
            .Select(id => $"SOS #{id}");

        return string.Join(", ", ids);
    }
}


