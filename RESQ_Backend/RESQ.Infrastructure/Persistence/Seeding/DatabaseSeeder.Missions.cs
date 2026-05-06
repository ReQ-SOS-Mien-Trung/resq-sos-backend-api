using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private async Task SeedMissionsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var missionClusters = seed.SosClusters.ToList();
        for (var i = 0; i < missionClusters.Count; i++)
        {
            var cluster = missionClusters[i];
            var createdAt = (cluster.CreatedAt ?? seed.StartUtc).AddMinutes(18);
            var status = i switch
            {
                0 or 3 => "Planned",
                1 or 4 or 6 => "OnGoing",
                2 or 5 => "Completed",
                _ => "Incompleted"
            };
            seed.Missions.Add(new Mission
            {
                ClusterId = cluster.Id,
                MissionType = MissionType(i, cluster.SeverityLevel),
                PriorityScore = PriorityScore(cluster.SeverityLevel ?? "Medium", i),
                Status = status,
                StartTime = status == "Planned" ? null : createdAt.AddMinutes(15),
                ExpectedEndTime = createdAt.AddHours(5 + i % 5),
                IsCompleted = status == "Completed",
                CreatedById = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = createdAt,
                CompletedAt = status == "Completed" ? createdAt.AddHours(5 + i % 5) : null
            });
        }

        _db.Missions.AddRange(seed.Missions);
        await _db.SaveChangesAsync(cancellationToken);

        var supplyItems = seed.ItemModels.Where(m => m.ItemType == "Consumable").Take(45).ToList();
        foreach (var mission in seed.Missions)
        {
            for (var j = 0; j < 2; j++)
            {
                var item = supplyItems[(mission.Id + j * 7) % supplyItems.Count];
                var inventory = seed.Inventories.First(i => i.ItemModelId == item.Id);
                _db.MissionItems.Add(new MissionItem
                {
                    MissionId = mission.Id,
                    ItemModelId = item.Id,
                    RequiredQuantity = 60 + (mission.Id + j) % 180,
                    AllocatedQuantity = 50 + (mission.Id + j) % 150,
                    SourceDepotId = inventory.DepotId,
                    BufferRatio = 0.10 + (j * 0.05)
                });
            }
        }

        for (var i = 0; i < seed.Missions.Count; i++)
        {
            var teamsForMission = i < 40 ? 2 : 1;
            for (var j = 0; j < teamsForMission; j++)
            {
                var team = TeamForMission(seed, i, j);
                var cluster = seed.SosClusters.First(c => c.Id == seed.Missions[i].ClusterId);
                var status = seed.Missions[i].Status switch
                {
                    "Completed" => i % 3 == 0 ? "Reported" : "CompletedWaitingReport",
                    "OnGoing" => "InProgress",
                    "Incompleted" => "Cancelled",
                    _ => "Assigned"
                };
                seed.MissionTeams.Add(new MissionTeam
                {
                    MissionId = seed.Missions[i].Id,
                    RescuerTeamId = team.Id,
                    TeamType = team.TeamType,
                    CurrentLocation = OffsetPoint(cluster.CenterLocation, 0.004 * (j + 1), -0.003 * (j + 1)),
                    LocationUpdatedAt = (seed.Missions[i].StartTime ?? seed.Missions[i].CreatedAt)?.AddMinutes(50),
                    LocationSource = "GPS",
                    Status = status,
                    AssignedAt = seed.Missions[i].CreatedAt?.AddMinutes(10 + j * 8),
                    UnassignedAt = status == "Cancelled" ? seed.Missions[i].CreatedAt?.AddHours(2) : null,
                    Note = "Giao đội theo năng lực và khoảng cách demo",
                    CreatedAt = seed.Missions[i].CreatedAt
                });
            }
        }

        _db.MissionTeams.AddRange(seed.MissionTeams);
        await _db.SaveChangesAsync(cancellationToken);

        SyncRescueTeamStatusesFromAssignments(seed);

        foreach (var missionTeam in seed.MissionTeams)
        {
            var sourceMembers = seed.RescueTeamMembers.Where(m => m.TeamId == missionTeam.RescuerTeamId).Take(5).ToList();
            foreach (var member in sourceMembers)
            {
                _db.MissionTeamMembers.Add(new MissionTeamMember
                {
                    MissionTeamId = missionTeam.Id,
                    RescuerId = member.UserId,
                    RoleInTeam = member.RoleInTeam,
                    JoinedAt = missionTeam.AssignedAt?.AddMinutes(5),
                    LeftAt = missionTeam.Status is "Reported" or "CompletedWaitingReport" ? missionTeam.AssignedAt?.AddHours(7) : null
                });
            }
        }

        foreach (var mission in seed.Missions)
        {
            var missionTeams = seed.MissionTeams.Where(t => t.MissionId == mission.Id).ToList();
            var missionIndex = seed.Missions.IndexOf(mission);
            var activities = mission.Status == "OnGoing" || missionIndex < 2 ? 5 : 4;
            var clusterSos = seed.SosRequests.Where(s => s.ClusterId == mission.ClusterId).ToList();
            for (var step = 1; step <= activities; step++)
            {
                var team = missionTeams[(step - 1) % missionTeams.Count];
                var sos = clusterSos[(step - 1) % clusterSos.Count];
                var type = ActivityType(step, activities, mission.MissionType);
                var hasDepot = type is "COLLECT_SUPPLIES" or "DELIVER_SUPPLIES" or "RETURN_SUPPLIES";
                var depot = hasDepot
                    ? OperationalDepotForActivity(seed, mission.Id, step)
                    : seed.Depots[(mission.Id + step) % seed.Depots.Count];
                var activityStatus = ActivityStatusFor(mission.Status, step, activities);
                var assigned = (mission.StartTime ?? mission.CreatedAt)?.AddMinutes(step * 35);
                seed.MissionActivities.Add(new MissionActivity
                {
                    MissionId = mission.Id,
                    Step = step,
                    ActivityType = type,
                    Description = ActivityDescription(type, depot.Name, sos.RawMessage),
                    Target = Json(new { address = SosAddressFromStructuredData(sos.StructuredData), sos_request_id = sos.Id }),
                    Items = hasDepot ? Json(new[] { new SupplyToCollectDto { ItemId = seed.ItemModels[(mission.Id + step) % seed.ItemModels.Count].Id, ItemName = seed.ItemModels[(mission.Id + step) % seed.ItemModels.Count].Name ?? "Vật phẩm", Quantity = 20 + step * 10, Unit = "đơn vị" } }) : null,
                    TargetLocation = hasDepot ? depot.Location : sos.Location,
                    Status = activityStatus,
                    AssignedAt = assigned,
                    CompletedAt = activityStatus is "Succeed" ? assigned?.AddMinutes(40 + step * 10) : null,
                    LastDecisionBy = seed.Coordinators[mission.Id % seed.Coordinators.Count].Id,
                    MissionTeamId = team.Id,
                    Priority = mission.PriorityScore >= 80 ? "Critical" : mission.PriorityScore >= 60 ? "High" : "Medium",
                    EstimatedTime = 35 + step * 15,
                    SosRequestId = sos.Id,
                    DepotId = hasDepot ? depot.Id : null,
                    DepotName = hasDepot ? depot.Name : null,
                    DepotAddress = hasDepot ? depot.Address : null,
                    AssemblyPointId = seed.RescueTeams.First(t => t.Id == team.RescuerTeamId).AssemblyPointId
                });
            }
        }

        _db.MissionActivities.AddRange(seed.MissionActivities);
        await _db.SaveChangesAsync(cancellationToken);

        await SeedTestActivityStatusesAsync(seed, cancellationToken);

        for (var i = 0; i < 35; i++)
        {
            var team = seed.MissionTeams.Where(t => t.Status is "Assigned" or "InProgress").ElementAt(i % seed.MissionTeams.Count(t => t.Status is "Assigned" or "InProgress"));
            var activity = seed.MissionActivities.First(a => a.MissionTeamId == team.Id);
            var support = i % 4 == 0 ? seed.SosRequests[(i * 9) % seed.SosRequests.Count] : null;
            var incident = new TeamIncident
            {
                MissionTeamId = team.Id,
                MissionActivityId = activity.Id,
                Location = OffsetPoint(activity.TargetLocation, 0.001 * (i % 3), -0.001 * (i % 2)),
                Description = IncidentDescription(i),
                Status = i % 3 == 0 ? "Resolved" : i % 3 == 1 ? "InProgress" : "Reported",
                IncidentScope = i % 2 == 0 ? "Activity" : "Mission",
                IncidentType = IncidentType(i),
                DecisionCode = i % 3 == 0 ? "COORDINATOR_REVIEWED" : null,
                DetailJson = Json(new { severity = i % 5 == 0 ? "High" : "Medium", weather = "mưa lớn", road = "ngập sâu" }),
                PayloadVersion = 1,
                NeedSupportSos = support is not null,
                NeedReassignActivity = i % 6 == 0,
                SupportSosRequestId = support?.Id,
                ReportedBy = seed.RescueTeamMembers.First(m => m.TeamId == team.RescuerTeamId).UserId,
                ReportedAt = activity.AssignedAt?.AddMinutes(45 + i)
            };
            _db.TeamIncidents.Add(incident);
            await _db.SaveChangesAsync(cancellationToken);
            _db.TeamIncidentActivities.Add(new TeamIncidentActivity
            {
                TeamIncidentId = incident.Id,
                MissionActivityId = activity.Id,
                OrderIndex = 1,
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }


    private static double PriorityScore(string priority, int index)
    {
        return priority switch
        {
            "Critical" => 88 + index % 12,
            "High" => 68 + index % 15,
            "Medium" => 42 + index % 18,
            _ => 20 + index % 18
        };
    }

    private static string MissionType(int index, string? severity)
    {
        if (severity == "Critical" && index % 2 == 0)
        {
            return "Mixed";
        }

        var types = new[] { "Rescue", "Medical", "Supply", "Mixed" };
        return types[index % types.Length];
    }

    private RescueTeam TeamForMission(DemoSeedContext seed, int missionIndex, int teamOffset)
    {
        var missionType = seed.Missions[missionIndex].MissionType;
        var required = missionType switch
        {
            "Medical" => "Medical",
            "Supply" => "Transportation",
            "Mixed" => "Mixed",
            _ => "Rescue"
        };
        var candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t) && t.TeamType == required && t.Status is "Available" or "Gathering")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t) && t.TeamType == required && t.Status != "Disbanded" && t.Status != "Unavailable")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t))
            .ToList();
        return candidates[(missionIndex + teamOffset) % candidates.Count];
    }

    private static void SyncRescueTeamStatusesFromAssignments(DemoSeedContext seed)
    {
        var activeMissionTeamsByRescueTeam = seed.MissionTeams
            .Where(team => team.RescuerTeamId.HasValue && team.UnassignedAt is null && team.Status != "Cancelled")
            .GroupBy(team => team.RescuerTeamId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var rescueTeam in seed.RescueTeams)
        {
            if (rescueTeam.Status is "Disbanded" or "Unavailable" or "Stuck")
            {
                continue;
            }

            if (!activeMissionTeamsByRescueTeam.TryGetValue(rescueTeam.Id, out var missionTeams))
            {
                rescueTeam.Status = rescueTeam.Status == "Gathering" ? "Gathering" : "Available";
                continue;
            }

            rescueTeam.Status = missionTeams.Any(team => team.Status == "InProgress")
                ? "OnMission"
                : missionTeams.Any(team => team.Status == "Assigned")
                    ? "Assigned"
                    : "Available";
        }
    }

    /// <summary>
    /// Post-processing: adjusts activities at Kho Huế (Depots[0]) to fixed demo-test statuses
    /// so that manager01 always has data for upcoming-returns, upcoming-pickups, confirm-return
    /// and confirm-pickup endpoints after every fresh seed.
    /// </summary>
    private async Task SeedTestActivityStatusesAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        if (seed.Depots.Count == 0) return;
        var hueDepotId = seed.Depots[0].Id; // Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế (manager@resq.vn)
        var onGoingMissionIds = seed.Missions
            .Where(mission => mission.Status == "OnGoing")
            .Select(mission => mission.Id)
            .ToHashSet();
        var inProgressMissionTeamIds = seed.MissionTeams
            .Where(team => team.Status == "InProgress")
            .Select(team => team.Id)
            .ToHashSet();

        // 1. Three RETURN_SUPPLIES → PendingConfirmation (for manager01 upcoming-returns + confirm-return)
        var returnActivities = seed.MissionActivities
            .Where(a => a.ActivityType == "RETURN_SUPPLIES"
                     && a.Status == "Planned"
                     && a.MissionId.HasValue
                     && onGoingMissionIds.Contains(a.MissionId.Value)
                     && a.MissionTeamId.HasValue
                     && inProgressMissionTeamIds.Contains(a.MissionTeamId.Value))
            .OrderBy(a => a.AssignedAt)
            .ThenBy(a => a.Id)
            .Take(3)
            .ToList();
        EnsureManagerUpcomingReturnFixtures(seed, seed.Depots[0], returnActivities);

        // 2. One COLLECT_SUPPLIES → OnGoing  (for upcoming-pickups + confirm-pickup)
        var pickupActivity = seed.MissionActivities
            .FirstOrDefault(a => a.DepotId == hueDepotId
                              && a.ActivityType == "COLLECT_SUPPLIES"
                              && a.Status == "Succeed");
        if (pickupActivity != null)
        {
            pickupActivity.Status = "OnGoing";
            pickupActivity.CompletedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SeedTestActivityStatuses: returnActivityIds={ReturnIds} -> PendingConfirmation; pickupActivityId={PickupId} -> OnGoing (hueDepotId={DepotId})",
            string.Join(",", returnActivities.Select(a => a.Id)), pickupActivity?.Id, hueDepotId);
    }

    private static void EnsureManagerUpcomingReturnFixtures(
        DemoSeedContext seed,
        Depot hueDepot,
        IReadOnlyList<MissionActivity> returnActivities)
    {
        if (returnActivities.Count < 3)
        {
            throw new InvalidOperationException(
                $"Migration demo seed requires at least 3 planned RETURN_SUPPLIES activities for depot #{hueDepot.Id}.");
        }

        var reusableUnitGroups = seed.ReusableItems
            .Where(item => item.Id > 30
                && item.DepotId == hueDepot.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal)
                && item.ItemModelId.HasValue
                && seed.ItemModels.Any(model =>
                    model.Id == item.ItemModelId.Value
                    && string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.ItemModelId!.Value)
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key)
            .ToList();

        if (reusableUnitGroups.Count < 2)
        {
            throw new InvalidOperationException(
                $"Migration demo seed requires reusable units for upcoming return fixtures at depot #{hueDepot.Id}.");
        }

        var reusableOnlyUnits = reusableUnitGroups[0].OrderBy(item => item.Id).Take(2).ToList();
        var mixedReusableUnit = reusableUnitGroups[1].OrderBy(item => item.Id).First();

        MarkUnitsInUse(reusableOnlyUnits.Concat([mixedReusableUnit]), seed.AnchorUtc);

        ConfigureReturnFixture(
            returnActivities[0],
            "Demo manager01 - trả thiết bị tái sử dụng",
            "Đơn demo manager01: đội trả thiết bị tái sử dụng về kho Huế, có serial cụ thể.",
            hueDepot,
            [
                BuildReusableReturnItem(seed, reusableOnlyUnits)
            ],
            assignedOffsetMinutes: 0);

        ConfigureReturnFixture(
            returnActivities[1],
            "Demo manager01 - trả vật phẩm tiêu hao theo lô",
            "Đơn demo manager01: đội trả vật phẩm tiêu hao dư thừa về kho Huế theo đúng lô FEFO.",
            hueDepot,
            BuildConsumableReturnItems(seed, hueDepot.Id,
            [
                ("Mì tôm", 20),
                ("Nước tinh khiết", 80),
                ("Thuốc hạ sốt Paracetamol 500mg", 120)
            ]),
            assignedOffsetMinutes: 20);

        var mixedItems = BuildConsumableReturnItems(seed, hueDepot.Id,
        [
            ("Nước tinh khiết", 12),
            ("Chăn ấm giữ nhiệt", 5)
        ]);
        mixedItems.Add(BuildReusableReturnItem(seed, [mixedReusableUnit]));

        ConfigureReturnFixture(
            returnActivities[2],
            "Demo manager01 - trả vật phẩm tiêu hao và thiết bị tái sử dụng",
            "Đơn demo manager01: đội trả cả vật phẩm tiêu hao theo lô và thiết bị tái sử dụng có serial.",
            hueDepot,
            mixedItems,
            assignedOffsetMinutes: 40);
    }

    private static void ConfigureReturnFixture(
        MissionActivity activity,
        string targetName,
        string description,
        Depot hueDepot,
        List<SupplyToCollectDto> items,
        int assignedOffsetMinutes)
    {
        var assignedAt = activity.AssignedAt ?? activity.Mission?.StartTime ?? activity.Mission?.CreatedAt;

        activity.ActivityType = "RETURN_SUPPLIES";
        activity.Description = description;
        activity.Target = Json(new { location = targetName, purpose = "manager01_upcoming_return_fixture" });
        activity.Items = Json(items);
        activity.TargetLocation = hueDepot.Location;
        activity.Status = "PendingConfirmation";
        activity.CompletedAt = null;
        activity.AssignedAt = assignedAt?.AddMinutes(assignedOffsetMinutes);
        activity.Priority = "Medium";
        activity.EstimatedTime = 30;
        activity.DepotId = hueDepot.Id;
        activity.DepotName = hueDepot.Name;
        activity.DepotAddress = hueDepot.Address;
    }

    private static List<SupplyToCollectDto> BuildConsumableReturnItems(
        DemoSeedContext seed,
        int depotId,
        IReadOnlyList<(string ItemName, int Quantity)> requests)
    {
        return requests
            .Select(request => BuildConsumableReturnItem(seed, depotId, request.ItemName, request.Quantity))
            .ToList();
    }

    private static SupplyToCollectDto BuildConsumableReturnItem(
        DemoSeedContext seed,
        int depotId,
        string itemName,
        int quantity)
    {
        var itemModel = seed.ItemModels.Single(model =>
            string.Equals(model.Name, itemName, StringComparison.OrdinalIgnoreCase));
        var inventory = seed.Inventories.Single(inventory =>
            inventory.DepotId == depotId && inventory.ItemModelId == itemModel.Id);

        var remaining = quantity;
        var allocations = new List<SupplyExecutionLotDto>();
        foreach (var lot in seed.Lots
            .Where(lot => lot.SupplyInventoryId == inventory.Id && lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.ExpiredDate ?? DateTime.MaxValue)
            .ThenBy(lot => lot.ReceivedDate ?? DateTime.MaxValue)
            .ThenBy(lot => lot.Id))
        {
            var take = Math.Min(remaining, lot.RemainingQuantity);
            if (take <= 0)
            {
                continue;
            }

            allocations.Add(new SupplyExecutionLotDto
            {
                LotId = lot.Id,
                QuantityTaken = take,
                ReceivedDate = lot.ReceivedDate,
                ExpiredDate = lot.ExpiredDate,
                RemainingQuantityAfterExecution = Math.Max(0, lot.RemainingQuantity - take)
            });

            remaining -= take;
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            throw new InvalidOperationException(
                $"Migration demo seed cannot allocate {quantity} units of '{itemName}' from depot #{depotId} lots.");
        }

        return new SupplyToCollectDto
        {
            ItemId = itemModel.Id,
            ItemName = itemModel.Name ?? itemName,
            ImageUrl = itemModel.ImageUrl,
            Quantity = quantity,
            Unit = itemModel.Unit,
            ExpectedReturnLotAllocations = allocations
        };
    }

    private static SupplyToCollectDto BuildReusableReturnItem(
        DemoSeedContext seed,
        IReadOnlyList<ReusableItem> units)
    {
        var itemModelId = units.Select(unit => unit.ItemModelId).Distinct().Single()
            ?? throw new InvalidOperationException("Reusable return fixture unit is missing ItemModelId.");
        var itemModel = seed.ItemModels.Single(model => model.Id == itemModelId);

        return new SupplyToCollectDto
        {
            ItemId = itemModel.Id,
            ItemName = itemModel.Name ?? $"Thiết bị #{itemModel.Id}",
            ImageUrl = itemModel.ImageUrl,
            Quantity = units.Count,
            Unit = itemModel.Unit,
            ExpectedReturnUnits = units
                .OrderBy(unit => unit.Id)
                .Select(unit => new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = unit.Id,
                    ItemModelId = itemModel.Id,
                    ItemName = itemModel.Name ?? $"Thiết bị #{itemModel.Id}",
                    SerialNumber = unit.SerialNumber,
                    Condition = unit.Condition,
                    Note = unit.Note
                })
                .ToList()
        };
    }

    private static void MarkUnitsInUse(IEnumerable<ReusableItem> units, DateTime updatedAt)
    {
        foreach (var unit in units)
        {
            unit.Status = ReusableItemStatus.InUse.ToString();
            unit.UpdatedAt = updatedAt;
            unit.Note = "Đang được đội giữ để trả về kho trong đơn RETURN_SUPPLIES demo manager01.";
        }
    }

    private static string ActivityType(int step, int total, string? missionType)
    {
        if (step == 1)
        {
            return "COLLECT_SUPPLIES";
        }

        if (step == total)
        {
            return "RETURN_SUPPLIES";
        }

        if (step == 2 && missionType is "Supply" or "Mixed")
        {
            return "DELIVER_SUPPLIES";
        }

        if (missionType == "Medical")
        {
            return "MEDICAL_AID";
        }

        return step % 2 == 0 ? "EVACUATE" : "RESCUE";
    }

    private static string ActivityStatusFor(string? missionStatus, int step, int total)
    {
        return missionStatus switch
        {
            "Completed" => "Succeed",
            "OnGoing" => step == 1 ? "Succeed" : step == total ? "Planned" : "OnGoing",
            "Incompleted" => step == total ? "Failed" : "Succeed",
            _ => "Planned"
        };
    }

    private static string ActivityDescription(string type, string? depotName, string? sosMessage)
    {
        return type switch
        {
            "COLLECT_SUPPLIES" => $"Di chuyển đến {depotName}, nhận nước uống, thuốc và áo phao.",
            "DELIVER_SUPPLIES" => "Giao vật phẩm cho hộ dân theo danh sách SOS.",
            "RETURN_SUPPLIES" => $"Hoàn trả áo phao, bộ đàm và dây cứu sinh về {depotName}.",
            "MEDICAL_AID" => "Sơ cứu tại chỗ, kiểm tra huyết áp và chuyển tuyến nếu cần.",
            "EVACUATE" => "Đưa người già, trẻ em ra điểm tránh trú an toàn.",
            _ => sosMessage ?? "Tiếp cận hiện trường và hỗ trợ cứu hộ."
        };
    }

    private static string IncidentDescription(int index)
    {
        var descriptions = new[]
        {
            "Xuồng bị kẹt rác ở chân cầu, cần hỗ trợ kéo ra.",
            "Đường vào khu dân cư nước chảy xiết, đội tạm dừng chờ điều phối.",
            "Một rescuer bị trượt chân xây xát nhẹ.",
            "Phát hiện thêm hộ dân bị cô lập phía sau trường mầm non.",
            "Bộ đàm mất tín hiệu trong 15 phút do mưa lớn."
        };
        return descriptions[index % descriptions.Length];
    }

    private static string IncidentType(int index)
    {
        var types = new[] { "VehicleIssue", "UnsafeRoute", "RescuerInjury", "AdditionalVictimsFound", "CommunicationLost" };
        return types[index % types.Length];
    }
}
