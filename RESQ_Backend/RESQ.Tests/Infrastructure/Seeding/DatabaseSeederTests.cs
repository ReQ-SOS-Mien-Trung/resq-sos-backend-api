using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Application.Services;
using System.Text.Json;

namespace RESQ.Tests.Infrastructure.Seeding;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_WithSameSeed_ProducesStableCountsAndSkipsSecondRun()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var seeder = CreateSeeder(context);

        await seeder.SeedAsync();
        var firstCounts = await CountsAsync(context);
        var validationErrors = await new DemoSeedValidator().ValidateAsync(context, CancellationToken.None);

        await seeder.SeedAsync();
        var secondCounts = await CountsAsync(context);

        Assert.Equal(firstCounts, secondCounts);
        Assert.Empty(validationErrors);
        Assert.Equal(296, firstCounts.Users);
        Assert.Equal(20, firstCounts.SosRequests);
        Assert.Equal(11, firstCounts.SosClusters);
        Assert.Equal(11, firstCounts.Missions);
        Assert.Equal(48, firstCounts.MissionActivities);
        Assert.Equal(140, firstCounts.Conversations);
        Assert.Equal(1900, firstCounts.Messages);
        Assert.Equal(841, firstCounts.SupplyInventories);
        Assert.Equal(95, firstCounts.SupplyRequests);
        Assert.Equal(2001, firstCounts.InventoryLogs);
        Assert.Equal(1, await context.SystemMigrationAudits.CountAsync(a => a.MigrationName == "demo-seed-v2-2026-04-24"));
        Assert.All(new[] { "Import", "Export", "TransferOut", "TransferIn", "Adjust", "Return" }, action =>
            Assert.True(context.InventoryLogs.Any(log => log.ActionType == action), $"Expected inventory log action {action}."));

        var unassignedRescuers = await context.Users
            .Where(u => u.RoleId == 3 && u.AssemblyPointId == null)
            .Where(u => !context.RescueTeamMembers.Any(member => member.UserId == u.Id))
            .Where(u => !context.AssemblyParticipants.Any(participant => participant.RescuerId == u.Id))
            .ToListAsync();
        var eligibleRescuerIds = await context.RescuerProfiles
            .Where(profile => profile.IsEligibleRescuer)
            .Select(profile => profile.UserId)
            .ToListAsync();

        Assert.Equal(140, await context.Users.CountAsync(u => u.RoleId == 3));
        Assert.Equal(30, unassignedRescuers.Count);
        foreach (var rescuer in unassignedRescuers)
        {
            Assert.Contains(rescuer.Id, eligibleRescuerIds);
        }

        var recentRescuerCutoff = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Utc);
        var seedAnchorUtc = SeedAnchorUtc();
        var recentRescuers = await context.Users
            .Where(u => u.RoleId == 3 && u.CreatedAt >= recentRescuerCutoff && u.CreatedAt <= seedAnchorUtc)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();
        var recentRescuerIds = recentRescuers.Select(u => u.Id).ToList();
        Assert.Equal(20, recentRescuers.Count);
        Assert.True(recentRescuers.Select(u => DateOnly.FromDateTime(u.CreatedAt!.Value)).Distinct().Count() >= 10);
        Assert.True(recentRescuers.Select(u => u.Province).Distinct().Count() >= 5);
        Assert.Equal(
            20,
            await context.RescuerProfiles.CountAsync(profile =>
                recentRescuerIds.Contains(profile.UserId)
                && profile.IsEligibleRescuer
                && profile.Step == 5
                && profile.ApprovedAt >= recentRescuerCutoff
                && profile.ApprovedAt <= seedAnchorUtc));

        var duplicateRescuerIdsAcrossNonDisbandedTeams = await context.RescueTeamMembers
            .Where(member => member.Team != null && member.Team.Status != "Disbanded")
            .GroupBy(member => member.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync();
        Assert.Empty(duplicateRescuerIdsAcrossNonDisbandedTeams);

        var rescueTeams = await context.RescueTeams.ToListAsync();
        var missionTeams = await context.MissionTeams.ToListAsync();
        Assert.All(
            rescueTeams.Where(team => team.Status == "Assigned"),
            team => Assert.Contains(
                missionTeams,
                missionTeam => missionTeam.RescuerTeamId == team.Id
                    && missionTeam.UnassignedAt == null
                    && missionTeam.Status == "Assigned"));
        Assert.All(
            rescueTeams.Where(team => team.Status == "OnMission"),
            team => Assert.Contains(
                missionTeams,
                missionTeam => missionTeam.RescuerTeamId == team.Id
                    && missionTeam.UnassignedAt == null
                    && missionTeam.Status == "InProgress"));

        var hueStadium = await context.AssemblyPoints
            .SingleAsync(point => point.Code == "AP-HUE-TD-241015");
        Assert.Equal("Sân vận động Tự Do (Thừa Thiên Huế)", hueStadium.Name);

        var serviceZones = await context.ServiceZones
            .OrderBy(zone => zone.Id)
            .ToListAsync();
        Assert.Equal(12, serviceZones.Count);
        Assert.Collection(
            serviceZones,
            zone => Assert.Equal("Thành phố Huế", zone.Name),
            zone => Assert.Equal("Thành phố Đà Nẵng", zone.Name),
            zone => Assert.Equal("Thành phố Hồ Chí Minh", zone.Name),
            zone => Assert.Equal("Tỉnh Thanh Hóa", zone.Name),
            zone => Assert.Equal("Tỉnh Nghệ An", zone.Name),
            zone => Assert.Equal("Tỉnh Hà Tĩnh", zone.Name),
            zone => Assert.Equal("Tỉnh Quảng Trị", zone.Name),
            zone => Assert.Equal("Tỉnh Quảng Ngãi", zone.Name),
            zone => Assert.Equal("Tỉnh Gia Lai", zone.Name),
            zone => Assert.Equal("Tỉnh Đắk Lắk", zone.Name),
            zone => Assert.Equal("Tỉnh Khánh Hòa", zone.Name),
            zone => Assert.Equal("Tỉnh Lâm Đồng", zone.Name));
        Assert.All(serviceZones, zone => Assert.False(string.IsNullOrWhiteSpace(zone.CoordinatesJson)));

        var overdueOpenEvents = await context.AssemblyEvents
            .Where(e => e.CheckInDeadline <= seedAnchorUtc && e.Status != "Completed")
            .ToListAsync();
        Assert.Empty(overdueOpenEvents);

        var hueActiveEvent = await context.AssemblyEvents
            .SingleAsync(e => e.AssemblyPointId == hueStadium.Id && e.Status == "Gathering");
        Assert.True(hueActiveEvent.CheckInDeadline > seedAnchorUtc);

        var hueAvailableTeams = await context.RescueTeams
            .Where(team => team.AssemblyPointId == hueStadium.Id && team.Status == "Available")
            .OrderBy(team => team.Code)
            .ToListAsync();
        Assert.Contains(hueAvailableTeams, team => team.Code == "RT-HUE-TD-AV-01");
        Assert.Contains(hueAvailableTeams, team => team.Code == "RT-HUE-TD-AV-02");

        var hueCheckedInStandbyRescuers = await context.Users
            .Where(u => u.RoleId == 3 && u.AssemblyPointId == hueStadium.Id)
            .Where(u => !context.RescueTeamMembers.Any(member => member.UserId == u.Id))
            .Where(u => !context.MissionTeamMembers.Any(member => member.RescuerId == u.Id))
            .Where(u => context.AssemblyParticipants.Any(participant =>
                participant.AssemblyEventId == hueActiveEvent.Id
                && participant.RescuerId == u.Id
                && participant.IsCheckedIn
                && !participant.IsCheckedOut))
            .ToListAsync();
        Assert.Equal(8, hueCheckedInStandbyRescuers.Count);

        var depotHue = await context.Depots.SingleAsync(depot => depot.Name == "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế");
        var depotDaNang = await context.Depots.SingleAsync(depot => depot.Name == "Ủy ban MTTQVN TP Đà Nẵng");
        var depotHaTinh = await context.Depots.SingleAsync(depot => depot.Name == "Ủy Ban MTTQ Tỉnh Hà Tĩnh");
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg", depotHue.ImageUrl);
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg", depotDaNang.ImageUrl);
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg", depotHaTinh.ImageUrl);

        var hueUpcomingReturns = await context.MissionActivities
            .Where(activity => activity.DepotId == depotHue.Id
                && activity.ActivityType == "RETURN_SUPPLIES"
                && activity.Status == "PendingConfirmation")
            .OrderBy(activity => activity.AssignedAt)
            .ThenBy(activity => activity.Id)
            .ToListAsync();
        Assert.Equal(3, hueUpcomingReturns.Count);

        var snapshotJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var hueReturnSnapshots = hueUpcomingReturns
            .Select(activity => JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items ?? "[]", snapshotJsonOptions) ?? [])
            .ToList();

        Assert.All(
            hueReturnSnapshots[0],
            item =>
            {
                Assert.NotEmpty(item.ExpectedReturnUnits ?? []);
                Assert.Empty(item.ExpectedReturnLotAllocations ?? []);
            });
        Assert.All(
            hueReturnSnapshots[1],
            item =>
            {
                Assert.NotEmpty(item.ExpectedReturnLotAllocations ?? []);
                Assert.Empty(item.ExpectedReturnUnits ?? []);
            });
        Assert.Contains(hueReturnSnapshots[2], item => (item.ExpectedReturnLotAllocations ?? []).Count > 0);
        Assert.Contains(hueReturnSnapshots[2], item => (item.ExpectedReturnUnits ?? []).Count > 0);

        var expectedReusableUnitIds = hueReturnSnapshots
            .SelectMany(items => items)
            .SelectMany(item => item.ExpectedReturnUnits ?? [])
            .Select(unit => unit.ReusableItemId)
            .ToList();
        Assert.Equal(expectedReusableUnitIds.Count, expectedReusableUnitIds.Distinct().Count());
        var expectedReusableUnits = await context.ReusableItems
            .Where(item => expectedReusableUnitIds.Contains(item.Id))
            .ToListAsync();
        Assert.Equal(expectedReusableUnitIds.Count, expectedReusableUnits.Count);
        Assert.All(expectedReusableUnits, unit => Assert.Equal("InUse", unit.Status));

        var depotFillRatios = await context.Depots
            .OrderBy(depot => depot.Id)
            .Select(depot => new
            {
                depot.Id,
                VolumeRatio = depot.Capacity > 0
                    ? Math.Round((depot.CurrentUtilization ?? 0m) / depot.Capacity!.Value, 2)
                    : 0m,
                WeightRatio = depot.WeightCapacity > 0
                    ? Math.Round((depot.CurrentWeightUtilization ?? 0m) / depot.WeightCapacity!.Value, 2)
                    : 0m
            })
            .ToListAsync();
        var expectedFillRatios = new[] { 0.95m, 0.70m, 0.33m, 0.95m, 0.70m, 0.33m, 0.95m, 0.90m, 0.50m };
        Assert.Equal(expectedFillRatios, depotFillRatios.Select(ratio => ratio.VolumeRatio));
        Assert.Equal(expectedFillRatios, depotFillRatios.Select(ratio => ratio.WeightRatio));

        var essentialDepotStock = await context.SupplyInventories
            .Include(inventory => inventory.ItemModel)
            .Where(inventory => inventory.ItemModel != null
                && (inventory.ItemModel.Name == "Áo phao cứu sinh" || inventory.ItemModel.Name == "Chăn ấm giữ nhiệt"))
            .OrderBy(inventory => inventory.DepotId)
            .ThenBy(inventory => inventory.ItemModel!.Name)
            .ToListAsync();
        Assert.Equal(await context.Depots.CountAsync() * 2, essentialDepotStock.Count);
        Assert.All(essentialDepotStock, inventory => Assert.InRange(inventory.Quantity ?? 0, 50, 100));

        var lifeJacketModelId = await context.ItemModels
            .Where(model => model.Name == "Áo phao cứu sinh")
            .Select(model => model.Id)
            .SingleAsync();
        var lifeJacketUnitsByDepot = await context.ReusableItems
            .Where(item => item.ItemModelId == lifeJacketModelId && item.DepotId.HasValue)
            .GroupBy(item => item.DepotId!.Value)
            .Select(group => new { DepotId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.DepotId, group => group.Count);
        Assert.All(
            essentialDepotStock.Where(inventory => inventory.ItemModel!.Name == "Áo phao cứu sinh"),
            inventory => Assert.Equal(inventory.Quantity, lifeJacketUnitsByDepot[inventory.DepotId!.Value]));

        var blanketInventoryIds = essentialDepotStock
            .Where(inventory => inventory.ItemModel!.Name == "Chăn ấm giữ nhiệt")
            .Select(inventory => inventory.Id)
            .ToList();
        var blanketLotRemainingByInventory = await context.SupplyInventoryLots
            .Where(lot => blanketInventoryIds.Contains(lot.SupplyInventoryId))
            .GroupBy(lot => lot.SupplyInventoryId)
            .Select(group => new { InventoryId = group.Key, Remaining = group.Sum(lot => lot.RemainingQuantity) })
            .ToDictionaryAsync(group => group.InventoryId, group => group.Remaining);
        Assert.All(
            essentialDepotStock.Where(inventory => inventory.ItemModel!.Name == "Chăn ấm giữ nhiệt"),
            inventory => Assert.Equal(inventory.Quantity, blanketLotRemainingByInventory[inventory.Id]));

        var incompleteRequestsForDepotOneAndTwo = await context.DepotSupplyRequests
            .Where(request => request.RequestingDepotId <= 2 || request.SourceDepotId <= 2)
            .Where(request => request.SourceStatus != "Completed" || request.RequestingStatus != "Received")
            .ToListAsync();
        Assert.Equal(12, incompleteRequestsForDepotOneAndTwo.Count);

        var incompleteRequestsForOtherDepots = await context.DepotSupplyRequests
            .Where(request => request.RequestingDepotId > 2 || request.SourceDepotId > 2)
            .Where(request => request.SourceStatus != "Completed" || request.RequestingStatus != "Received")
            .ToListAsync();
        Assert.Empty(incompleteRequestsForOtherDepots);

        var closureDepotNames = new[] { "Kho cứu trợ Đại học Phú Yên", "Ga đường sắt Sài Gòn" };
        var closureTestDepots = await context.Depots
            .Where(depot => closureDepotNames.Contains(depot.Name!))
            .OrderBy(depot => depot.Id)
            .ToListAsync();
        Assert.Equal(closureDepotNames.Length, closureTestDepots.Count);

        var totalItemModelCount = await context.ItemModels.CountAsync();
        foreach (var closureTestDepot in closureTestDepots)
        {
            Assert.False(await context.DepotSupplyRequests.AnyAsync(request =>
                request.RequestingDepotId == closureTestDepot.Id || request.SourceDepotId == closureTestDepot.Id));
            Assert.False(await context.MissionActivities.AnyAsync(activity =>
                activity.DepotId == closureTestDepot.Id
                && (activity.ActivityType == "COLLECT_SUPPLIES"
                    || activity.ActivityType == "DELIVER_SUPPLIES"
                    || activity.ActivityType == "RETURN_SUPPLIES")));

            var closureInventories = await context.SupplyInventories
                .Include(inventory => inventory.ItemModel)
                .Where(inventory => inventory.DepotId == closureTestDepot.Id)
                .OrderBy(inventory => inventory.ItemModelId)
                .ToListAsync();
            Assert.Equal(totalItemModelCount, closureInventories.Count);
            Assert.All(closureInventories, inventory => Assert.True((inventory.Quantity ?? 0) > 0));
            Assert.All(closureInventories, inventory => Assert.Equal(0, inventory.MissionReservedQuantity));
            Assert.All(closureInventories, inventory => Assert.Equal(0, inventory.TransferReservedQuantity));

            var closureConsumableInventoryIds = closureInventories
                .Where(inventory => inventory.ItemModel!.ItemType == "Consumable")
                .Select(inventory => inventory.Id)
                .ToList();
            var closureLotRemainingByInventory = await context.SupplyInventoryLots
                .Where(lot => closureConsumableInventoryIds.Contains(lot.SupplyInventoryId))
                .GroupBy(lot => lot.SupplyInventoryId)
                .Select(group => new { InventoryId = group.Key, Remaining = group.Sum(lot => lot.RemainingQuantity) })
                .ToDictionaryAsync(group => group.InventoryId, group => group.Remaining);
            Assert.Equal(closureConsumableInventoryIds.Count, closureLotRemainingByInventory.Count);
            Assert.All(
                closureInventories.Where(inventory => inventory.ItemModel!.ItemType == "Consumable"),
                inventory => Assert.Equal(inventory.Quantity, closureLotRemainingByInventory[inventory.Id]));

            var closureReusableUnitsByModel = await context.ReusableItems
                .Where(item => item.DepotId == closureTestDepot.Id && item.ItemModelId.HasValue)
                .GroupBy(item => item.ItemModelId!.Value)
                .Select(group => new { ItemModelId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.ItemModelId, group => group.Count);
            Assert.All(
                closureInventories.Where(inventory => inventory.ItemModel!.ItemType == "Reusable"),
                inventory => Assert.Equal(inventory.Quantity, closureReusableUnitsByModel[inventory.ItemModelId!.Value]));
            Assert.False(await context.ReusableItems.AnyAsync(item =>
                item.DepotId == closureTestDepot.Id
                && item.Status != "Available"));
        }

        Assert.False(await context.SupplyInventories.AnyAsync(inventory =>
            inventory.DepotId == 1
            && inventory.ItemModel != null
            && inventory.ItemModel.Name == "Pin dự phòng 10000mAh"));
        Assert.False(await context.ReusableItems.AnyAsync(item =>
            item.DepotId == 1
            && item.ItemModel != null
            && item.ItemModel.Name == "Bộ đèn pin đội đầu"));
        Assert.Empty(await context.SupplyInventoryLots
            .Where(lot => lot.SupplyInventory.DepotId == 1
                && lot.SupplyInventory.ItemModel != null
                && lot.SupplyInventory.ItemModel.Name == "Pin dự phòng 10000mAh")
            .ToListAsync());
        Assert.All(
            await context.InventoryLogs
                .Where(log => log.ReusableItemId != null && log.ActionType == "Import")
                .ToListAsync(),
            log => Assert.Equal(log.CreatedAt, log.ReceivedDate));

        var depotFundCounts = await context.DepotFunds
            .GroupBy(fund => fund.DepotId)
            .OrderBy(group => group.Key)
            .Select(group => group.Count())
            .ToListAsync();
        Assert.Equal(new[] { 3, 2, 1, 3, 2, 1, 1, 3, 2 }, depotFundCounts);
        Assert.All(
            await context.DepotFunds.ToListAsync(),
            fund => Assert.True(
                context.DepotFundTransactions.Any(transaction => transaction.DepotFundId == fund.Id),
                $"Expected depot fund #{fund.Id} to have transactions."));

        var stadiumSos = await context.SosRequests
            .Where(s => s.Location != null)
            .OrderBy(s => s.Id)
            .ToListAsync();
        var clusteredSosRows = await context.SosRequests
            .Where(s => s.ClusterId != null)
            .ToListAsync();
        var clusterCapacitySnapshots = clusteredSosRows
            .GroupBy(s => s.ClusterId!.Value)
            .Select(group =>
            {
                var priorities = group.Select(s => s.PriorityLevel).ToList();
                return new
                {
                    ClusterId = group.Key,
                    Count = group.Count(),
                    HighestPriority = HighestSosPriority(priorities),
                    Limit = MaxSosRequestsForCluster(priorities)
                };
            })
            .ToList();
        var priorityCounts = await context.SosRequests
            .GroupBy(s => s.PriorityLevel)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key ?? string.Empty, group => group.Count);
        var sosTypeCounts = await context.SosRequests
            .GroupBy(s => s.SosType)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key ?? string.Empty, group => group.Count);
        var recentOpenSos = await context.SosRequests
            .Where(s => s.Status == "Pending" || s.Status == "Assigned" || s.Status == "InProgress" || s.Status == "Incident")
            .Select(s => new { s.Id, s.Status, s.CreatedAt, s.ReceivedAt, s.ReviewedAt, s.LastUpdatedAt })
            .ToListAsync();
        var sosPayloads = await context.SosRequests
            .Where(s => s.StructuredData != null)
            .Select(s => new { s.SosType, s.StructuredData })
            .ToListAsync();
        var mobilePackets = await context.SosRequests
            .Select(s => new { s.RawMessage, s.NetworkMetadata, s.SenderInfo, s.ReporterInfo, s.VictimInfo, s.OriginId, s.Timestamp, s.CreatedAt })
            .ToListAsync();
        var completedClusterIds = await context.SosClusters
            .Where(cluster => cluster.Status == "Completed")
            .Select(cluster => cluster.Id)
            .ToListAsync();
        var mobilePacketMissingCount = await context.SosRequests.CountAsync(s =>
            string.IsNullOrWhiteSpace(s.RawMessage)
            || s.Location == null
            || string.IsNullOrWhiteSpace(s.StructuredData)
            || string.IsNullOrWhiteSpace(s.NetworkMetadata)
            || string.IsNullOrWhiteSpace(s.SenderInfo)
            || string.IsNullOrWhiteSpace(s.ReporterInfo)
            || string.IsNullOrWhiteSpace(s.VictimInfo)
            || string.IsNullOrWhiteSpace(s.OriginId)
            || s.PacketId == null);

        Assert.Equal(20, stadiumSos.Count);
        Assert.DoesNotContain(stadiumSos, s => s.ClusterId == null);
        Assert.Equal(11, clusterCapacitySnapshots.Count);
        Assert.All(clusterCapacitySnapshots, cluster =>
            Assert.True(
                cluster.Count <= cluster.Limit,
                $"Cluster #{cluster.ClusterId} contains {cluster.Count} SOS requests; {cluster.HighestPriority} clusters allow at most {cluster.Limit}."));
        Assert.Contains(clusterCapacitySnapshots, cluster => cluster.HighestPriority == "Critical" && cluster.Count == 1 && cluster.Limit == 1);
        Assert.Contains(clusterCapacitySnapshots, cluster => cluster.HighestPriority == "High" && cluster.Count == 2 && cluster.Limit == 2);
        Assert.Contains(clusterCapacitySnapshots, cluster => cluster.HighestPriority == "Medium" && cluster.Count == 3 && cluster.Limit == 3);
        Assert.Contains(clusterCapacitySnapshots, cluster => cluster.HighestPriority == "Low" && cluster.Count == 2 && cluster.Limit == 5);
        Assert.Equal(2, completedClusterIds.Count);
        Assert.False(await context.SosRequests.AnyAsync(s =>
            s.ClusterId.HasValue
            && completedClusterIds.Contains(s.ClusterId.Value)
            && s.Status != "Resolved"));
        Assert.Equal(0, mobilePacketMissingCount);
        Assert.All(mobilePackets, packet =>
        {
            Assert.StartsWith("[", packet.RawMessage);
            Assert.Contains("| Tình trạng:", packet.RawMessage);
            Assert.Contains("| Số người:", packet.RawMessage);
            Assert.Contains("| Ghi chú:", packet.RawMessage);
            Assert.True(Guid.TryParse(packet.OriginId, out _));
            Assert.NotNull(packet.CreatedAt);
            Assert.Equal(new DateTimeOffset(packet.CreatedAt!.Value).ToUnixTimeSeconds(), packet.Timestamp);

            using var network = JsonDocument.Parse(packet.NetworkMetadata!);
            Assert.True(network.RootElement.TryGetProperty("hop_count", out _));
            var path = network.RootElement.GetProperty("path").EnumerateArray().Select(item => item.GetString()).ToList();
            Assert.Contains(packet.OriginId, path);

            using var sender = JsonDocument.Parse(packet.SenderInfo!);
            Assert.False(string.IsNullOrWhiteSpace(sender.RootElement.GetProperty("device_id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(sender.RootElement.GetProperty("user_name").GetString()));
            Assert.Matches("^(0|\\+84)[0-9]{9}$", sender.RootElement.GetProperty("user_phone").GetString() ?? "");
            Assert.True(sender.RootElement.TryGetProperty("is_online", out _));

            using var reporter = JsonDocument.Parse(packet.ReporterInfo!);
            Assert.False(string.IsNullOrWhiteSpace(reporter.RootElement.GetProperty("device_id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(reporter.RootElement.GetProperty("user_name").GetString()));
            Assert.Matches("^(0|\\+84)[0-9]{9}$", reporter.RootElement.GetProperty("user_phone").GetString() ?? "");

            using var victimInfo = JsonDocument.Parse(packet.VictimInfo!);
            Assert.False(string.IsNullOrWhiteSpace(victimInfo.RootElement.GetProperty("user_name").GetString()));
            Assert.Matches("^\\+84[0-9]{9}$", victimInfo.RootElement.GetProperty("user_phone").GetString() ?? "");
        });
        Assert.All(stadiumSos, sos =>
        {
            Assert.InRange(sos.Location!.Y, 16.457, 16.4675);
            Assert.InRange(sos.Location!.X, 107.598, 107.6075);
        });
        Assert.All(await context.SosClusters.Where(c => c.CenterLocation != null).Select(c => c.CenterLocation!).ToListAsync(), point =>
        {
            Assert.InRange(point.Y, 16.457, 16.4675);
            Assert.InRange(point.X, 107.598, 107.6075);
        });
        Assert.DoesNotContain(await context.SosRequests.Select(s => s.PriorityLevel).Distinct().ToListAsync(), value => value == "Moderate");
        Assert.Equal(3, priorityCounts.GetValueOrDefault("Critical"));
        Assert.Equal(8, priorityCounts.GetValueOrDefault("High"));
        Assert.Equal(7, priorityCounts.GetValueOrDefault("Medium"));
        Assert.Equal(2, priorityCounts.GetValueOrDefault("Low"));
        Assert.Equal(9, sosTypeCounts.GetValueOrDefault("Rescue"));
        Assert.Equal(5, sosTypeCounts.GetValueOrDefault("Relief"));
        Assert.Equal(6, sosTypeCounts.GetValueOrDefault("Both"));
        Assert.Contains(sosPayloads, payload => payload.SosType == "Relief");
        Assert.Contains(sosPayloads, payload => payload.SosType == "Rescue");
        Assert.Contains(sosPayloads, payload => payload.SosType == "Both");
        Assert.All(sosPayloads, payload =>
        {
            using var payloadDocument = JsonDocument.Parse(payload.StructuredData!);
            var root = payloadDocument.RootElement;
            var incident = root.GetProperty("incident");

            Assert.False(string.IsNullOrWhiteSpace(incident.GetProperty("address").GetString()));
            var peopleCount = incident.GetProperty("people_count");
            Assert.True(peopleCount.GetProperty("adult").GetInt32()
                + peopleCount.GetProperty("child").GetInt32()
                + peopleCount.GetProperty("elderly").GetInt32() > 0);
            Assert.True(incident.TryGetProperty("can_move", out _));
            Assert.True(incident.TryGetProperty("has_injured", out _));
            Assert.True(incident.TryGetProperty("need_medical", out _));
            Assert.True(incident.TryGetProperty("others_are_stable", out _));
            Assert.False(string.IsNullOrWhiteSpace(incident.GetProperty("additional_description").GetString()));
            Assert.True(IsCapsLockToken(incident.GetProperty("situation").GetString()));

            var victims = root.GetProperty("victims").EnumerateArray().ToList();
            Assert.NotEmpty(victims);
            Assert.All(victims, victim =>
            {
                Assert.False(string.IsNullOrWhiteSpace(victim.GetProperty("person_id").GetString()));
                Assert.True(IsCapsLockToken(victim.GetProperty("person_type").GetString()));
                Assert.True(victim.GetProperty("index").GetInt32() > 0);
                Assert.False(string.IsNullOrWhiteSpace(victim.GetProperty("custom_name").GetString()));
                var incidentStatus = victim.GetProperty("incident_status");
                Assert.True(incidentStatus.TryGetProperty("is_injured", out _));
                Assert.True(IsCapsLockToken(incidentStatus.GetProperty("severity").GetString()));
                Assert.Equal(JsonValueKind.Array, incidentStatus.GetProperty("medical_issues").ValueKind);
                var personalNeeds = victim.GetProperty("personal_needs");
                Assert.True(personalNeeds.GetProperty("clothing").TryGetProperty("needed", out _));
                Assert.True(personalNeeds.GetProperty("diet").TryGetProperty("has_special_diet", out _));
            });

            if (payload.SosType is "Relief" or "Both")
            {
                var groupNeeds = root.GetProperty("group_needs");
                Assert.NotEmpty(groupNeeds.GetProperty("supplies").EnumerateArray());
                Assert.True(groupNeeds.TryGetProperty("water", out _));
                Assert.True(groupNeeds.TryGetProperty("food", out _));
                Assert.True(groupNeeds.TryGetProperty("medicine", out _));
            }
        });
        Assert.Equal(20, await context.SosRuleEvaluations.Select(evaluation => evaluation.SosRequestId).Distinct().CountAsync());
        Assert.Equal(20, await context.SosAiAnalyses.Select(analysis => analysis.SosRequestId).Distinct().CountAsync());
        Assert.Equal(20, await context.SosRequestUpdates.Select(update => update.SosRequestId).Distinct().CountAsync());
        Assert.Equal(20, await context.SosRequestCompanions.Select(companion => companion.SosRequestId).Distinct().CountAsync());
        Assert.NotEmpty(recentOpenSos);
        Assert.All(recentOpenSos, sos =>
        {
            Assert.NotNull(sos.CreatedAt);
            Assert.NotNull(sos.ReceivedAt);
            Assert.NotNull(sos.LastUpdatedAt);
            Assert.InRange(sos.CreatedAt!.Value, seedAnchorUtc.AddHours(-24), seedAnchorUtc);
            Assert.InRange(sos.ReceivedAt!.Value, sos.CreatedAt.Value, seedAnchorUtc);
            Assert.InRange(sos.LastUpdatedAt!.Value, sos.ReceivedAt.Value, seedAnchorUtc);

            if (sos.Status == "Pending")
            {
                Assert.Null(sos.ReviewedAt);
            }
            else
            {
                Assert.NotNull(sos.ReviewedAt);
                Assert.InRange(sos.ReviewedAt!.Value, sos.CreatedAt.Value, seedAnchorUtc);
                Assert.InRange(sos.LastUpdatedAt.Value, sos.ReviewedAt.Value, seedAnchorUtc);
            }
        });
        Assert.Contains(await context.Missions.Select(m => m.Status).Distinct().ToListAsync(), status => status == "Planned");
        Assert.Contains(await context.Missions.Select(m => m.Status).Distinct().ToListAsync(), status => status == "OnGoing");
        Assert.Contains(await context.Missions.Select(m => m.Status).Distinct().ToListAsync(), status => status == "Completed");
        Assert.Contains(await context.Missions.Select(m => m.Status).Distinct().ToListAsync(), status => status == "Incompleted");
        Assert.All(
            await context.SosRequests.Select(s => s.SosType).Distinct().ToListAsync(),
            sosType => Assert.False(IsCapsLockToken(sosType), $"Expected PascalCase sos_type but found '{sosType}'."));
        Assert.All(
            await context.Missions.Select(m => m.MissionType).Distinct().ToListAsync(),
            missionType => Assert.False(IsCapsLockToken(missionType), $"Expected PascalCase mission_type but found '{missionType}'."));
        Assert.All(
            await context.Users.Where(u => u.RoleId == 5).Select(u => u.Phone).ToListAsync(),
            phone => Assert.Matches("^\\+84[0-9]{9}$", phone ?? ""));
        Assert.All(
            await context.Users.Where(u => u.RoleId != 5).Select(u => u.Phone).ToListAsync(),
            phone => Assert.Matches("^0[0-9]{9}$", phone ?? ""));

        var demoVictim = await context.Users.SingleAsync(u => u.Phone == "+84374745872");
        Assert.Equal(5, demoVictim.RoleId);
        Assert.False(demoVictim.IsBanned);
        Assert.True(BCrypt.Net.BCrypt.Verify("142200", demoVictim.Password));

        var demoVictimRelatives = await context.UserRelativeProfiles
            .Where(profile => profile.UserId == demoVictim.Id)
            .OrderBy(profile => profile.DisplayName)
            .ToListAsync();
        Assert.Equal(4, demoVictimRelatives.Count);
        Assert.Contains(demoVictimRelatives, profile => profile.DisplayName == "Châu" && profile.PersonType == "ELDERLY" && profile.RelationGroup == "gia_dinh");
        Assert.Contains(demoVictimRelatives, profile => profile.DisplayName == "An" && profile.PersonType == "ADULT" && profile.Gender == "FEMALE");
        Assert.Contains(demoVictimRelatives, profile => profile.DisplayName == "Thảo" && profile.TagsJson.Contains("biet_so_cuu", StringComparison.Ordinal));
        Assert.Contains(demoVictimRelatives, profile => profile.DisplayName == "Khoa" && profile.PhoneNumber == "+84911224567");
        Assert.All(demoVictimRelatives, profile =>
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.MedicalProfileJson));
            using var medicalProfile = JsonDocument.Parse(profile.MedicalProfileJson);
            Assert.True(medicalProfile.RootElement.TryGetProperty("bloodType", out _));
            Assert.True(medicalProfile.RootElement.TryGetProperty("specialSituation", out _));
        });

        Assert.All(await context.SosRequests.Where(s => s.Location != null).Select(s => s.Location!).Take(40).ToListAsync(), point =>
        {
            Assert.InRange(point.Y, 16.457, 16.4675);
            Assert.InRange(point.X, 107.598, 107.6075);
        });
    }

    [Fact]
    public async Task SeedAsync_ProducesStableSosCoordinatesAcrossFreshContexts()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        await firstContext.Database.EnsureCreatedAsync();
        await secondContext.Database.EnsureCreatedAsync();

        await CreateSeeder(firstContext).SeedAsync();
        await CreateSeeder(secondContext).SeedAsync();

        var firstSnapshot = await LoadSosCoordinateSnapshotsAsync(firstContext);
        var secondSnapshot = await LoadSosCoordinateSnapshotsAsync(secondContext);

        Assert.Equal(firstSnapshot, secondSnapshot);
    }

    [Fact]
    public async Task SeedAsync_CreatesManager01UpcomingReturnFixturesForHueDepot()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var seeder = CreateSeeder(context, failOnValidationError: false);

        await seeder.SeedAsync();

        var depotHue = await context.Depots.SingleAsync(depot => depot.Id == 1);
        var hueUpcomingReturns = await context.MissionActivities
            .Where(activity => activity.DepotId == depotHue.Id
                && activity.ActivityType == "RETURN_SUPPLIES"
                && activity.Status == "PendingConfirmation")
            .OrderBy(activity => activity.AssignedAt)
            .ThenBy(activity => activity.Id)
            .ToListAsync();

        Assert.Equal(3, hueUpcomingReturns.Count);
        Assert.DoesNotContain(hueUpcomingReturns, activity => activity.Status == "Succeed");

        var snapshotJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var hueReturnSnapshots = hueUpcomingReturns
            .Select(activity => JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items ?? "[]", snapshotJsonOptions) ?? [])
            .ToList();

        Assert.All(
            hueReturnSnapshots[0],
            item =>
            {
                Assert.NotEmpty(item.ExpectedReturnUnits ?? []);
                Assert.Empty(item.ExpectedReturnLotAllocations ?? []);
            });
        Assert.All(
            hueReturnSnapshots[1],
            item =>
            {
                Assert.NotEmpty(item.ExpectedReturnLotAllocations ?? []);
                Assert.Empty(item.ExpectedReturnUnits ?? []);
            });
        Assert.Contains(hueReturnSnapshots[2], item => (item.ExpectedReturnLotAllocations ?? []).Count > 0);
        Assert.Contains(hueReturnSnapshots[2], item => (item.ExpectedReturnUnits ?? []).Count > 0);

        var expectedReusableUnitIds = hueReturnSnapshots
            .SelectMany(items => items)
            .SelectMany(item => item.ExpectedReturnUnits ?? [])
            .Select(unit => unit.ReusableItemId)
            .ToList();
        Assert.Equal(expectedReusableUnitIds.Count, expectedReusableUnitIds.Distinct().Count());

        var expectedReusableUnits = await context.ReusableItems
            .Where(item => expectedReusableUnitIds.Contains(item.Id))
            .ToListAsync();
        Assert.Equal(expectedReusableUnitIds.Count, expectedReusableUnits.Count);
        Assert.All(expectedReusableUnits, unit => Assert.Equal("InUse", unit.Status));
    }

    [Fact]
    public async Task SeedAsync_CreatesExpiringConsumableLotsForHueDepot()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        await CreateSeeder(context).SeedAsync();

        var anchorUtc = SeedAnchorUtc();
        var expiringThreshold = anchorUtc.AddDays(30);
        var expectedLots = new[]
        {
            new { ItemName = "Mì tôm", Quantity = 24, SourceId = 90_001, ReceivedDate = anchorUtc.AddDays(-20), ExpiredDate = anchorUtc.AddDays(7) },
            new { ItemName = "Nước tinh khiết", Quantity = 48, SourceId = 90_002, ReceivedDate = anchorUtc.AddDays(-18), ExpiredDate = anchorUtc.AddDays(14) },
            new { ItemName = "Sữa bột trẻ em", Quantity = 18, SourceId = 90_003, ReceivedDate = anchorUtc.AddDays(-16), ExpiredDate = anchorUtc.AddDays(21) },
            new { ItemName = "Thuốc hạ sốt Paracetamol 500mg", Quantity = 60, SourceId = 90_004, ReceivedDate = anchorUtc.AddDays(-14), ExpiredDate = anchorUtc.AddDays(28) }
        };

        foreach (var expected in expectedLots)
        {
            var inventory = await context.SupplyInventories
                .Include(item => item.ItemModel)
                .Include(item => item.Lots)
                .Include(item => item.InventoryLogs)
                .SingleAsync(item =>
                    item.DepotId == 1
                    && item.ItemModel != null
                    && item.ItemModel.Name == expected.ItemName);

            var expiringLot = Assert.Single(
                inventory.Lots,
                lot => lot.Quantity == expected.Quantity
                    && lot.RemainingQuantity == expected.Quantity
                    && lot.SourceType == "Purchase"
                    && lot.SourceId == expected.SourceId);

            Assert.Equal(expected.ReceivedDate, expiringLot.ReceivedDate);
            Assert.Equal(expected.ExpiredDate, expiringLot.ExpiredDate);
            Assert.InRange(expiringLot.ExpiredDate!.Value, anchorUtc, expiringThreshold);
            Assert.Equal(inventory.Quantity, inventory.Lots.Sum(lot => lot.RemainingQuantity));

            var importLog = Assert.Single(
                inventory.InventoryLogs,
                log => log.ActionType == "Import"
                    && log.SupplyInventoryLotId == expiringLot.Id
                    && log.QuantityChange == expected.Quantity
                    && log.SourceType == "Purchase"
                    && log.SourceId == expected.SourceId);

            Assert.Equal(expected.ReceivedDate, importLog.ReceivedDate);
            Assert.Equal(expected.ExpiredDate, importLog.ExpiredDate);
        }
    }

    [Fact]
    public async Task SeedAsync_KeepsOperationalAuditTimestampsWithinSeedAnchor()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        await CreateSeeder(context).SeedAsync();

        var seedAnchorUtc = SeedAnchorUtc();
        var futureInventoryLogs = await context.InventoryLogs
            .Select(log => new { log.Id, CreatedAt = (DateTime?)log.CreatedAt, log.ReceivedDate })
            .Where(log => log.CreatedAt > seedAnchorUtc || log.ReceivedDate > seedAnchorUtc)
            .ToListAsync();
        var futureSupplyRequestActuals = await context.DepotSupplyRequests
            .Select(request => new
            {
                request.Id,
                request.HighEscalationNotifiedAt,
                request.UrgentEscalationNotifiedAt,
                request.RespondedAt,
                request.ShippedAt,
                request.CompletedAt,
                request.UpdatedAt
            })
            .Where(request =>
                request.HighEscalationNotifiedAt > seedAnchorUtc
                || request.UrgentEscalationNotifiedAt > seedAnchorUtc
                || request.RespondedAt > seedAnchorUtc
                || request.ShippedAt > seedAnchorUtc
                || request.CompletedAt > seedAnchorUtc
                || request.UpdatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureAssemblyAudit = await context.AssemblyEvents
            .Select(assemblyEvent => new { assemblyEvent.Id, CreatedAt = (DateTime?)assemblyEvent.CreatedAt, UpdatedAt = (DateTime?)assemblyEvent.UpdatedAt })
            .Where(assemblyEvent => assemblyEvent.CreatedAt > seedAnchorUtc || assemblyEvent.UpdatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureAssemblyParticipantAudit = await context.AssemblyParticipants
            .Select(participant => new { participant.AssemblyEventId, participant.RescuerId, participant.CheckInTime, participant.CheckOutTime })
            .Where(participant => participant.CheckInTime > seedAnchorUtc || participant.CheckOutTime > seedAnchorUtc)
            .ToListAsync();
        var futureSosAudit = await context.SosRequests
            .Select(sos => new { sos.Id, CreatedAt = (DateTime?)sos.CreatedAt, sos.ReceivedAt, sos.ReviewedAt, sos.LastUpdatedAt })
            .Where(sos => sos.CreatedAt > seedAnchorUtc || sos.ReceivedAt > seedAnchorUtc || sos.ReviewedAt > seedAnchorUtc || sos.LastUpdatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureClusterAudit = await context.SosClusters
            .Select(cluster => new { cluster.Id, CreatedAt = (DateTime?)cluster.CreatedAt, LastUpdatedAt = (DateTime?)cluster.LastUpdatedAt })
            .Where(cluster => cluster.CreatedAt > seedAnchorUtc || cluster.LastUpdatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureDerivedSosAudit = await context.SosRequestCompanions
            .Select(companion => new { companion.SosRequestId, companion.UserId, AddedAt = (DateTime?)companion.AddedAt })
            .Where(companion => companion.AddedAt > seedAnchorUtc)
            .ToListAsync();
        var futureRuleEvaluations = await context.SosRuleEvaluations
            .Select(evaluation => new { evaluation.SosRequestId, CreatedAt = (DateTime?)evaluation.CreatedAt })
            .Where(evaluation => evaluation.CreatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureSosUpdates = await context.SosRequestUpdates
            .Select(update => new { update.SosRequestId, CreatedAt = (DateTime?)update.CreatedAt })
            .Where(update => update.CreatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureSosAi = await context.SosAiAnalyses
            .Select(analysis => new { analysis.SosRequestId, CreatedAt = (DateTime?)analysis.CreatedAt, analysis.AdoptedAt })
            .Where(analysis => analysis.CreatedAt > seedAnchorUtc || analysis.AdoptedAt > seedAnchorUtc)
            .ToListAsync();
        var futureConversationAudit = await context.Conversations
            .Select(conversation => new { conversation.Id, conversation.CreatedAt, conversation.UpdatedAt })
            .Where(conversation => conversation.CreatedAt > seedAnchorUtc || conversation.UpdatedAt > seedAnchorUtc)
            .ToListAsync();
        var futureConversationParticipants = await context.ConversationParticipants
            .Select(participant => new { participant.ConversationId, participant.UserId, participant.JoinedAt, participant.LeftAt })
            .Where(participant => participant.JoinedAt > seedAnchorUtc || participant.LeftAt > seedAnchorUtc)
            .ToListAsync();
        var futureMessages = await context.Messages
            .Select(message => new { message.Id, CreatedAt = (DateTime?)message.CreatedAt })
            .Where(message => message.CreatedAt > seedAnchorUtc)
            .ToListAsync();

        Assert.Empty(futureInventoryLogs);
        Assert.Empty(futureSupplyRequestActuals);
        Assert.Empty(futureAssemblyAudit);
        Assert.Empty(futureAssemblyParticipantAudit);
        Assert.Empty(futureSosAudit);
        Assert.Empty(futureClusterAudit);
        Assert.Empty(futureDerivedSosAudit);
        Assert.Empty(futureRuleEvaluations);
        Assert.Empty(futureSosUpdates);
        Assert.Empty(futureSosAi);
        Assert.Empty(futureConversationAudit);
        Assert.Empty(futureConversationParticipants);
        Assert.Empty(futureMessages);

        Assert.True(await context.SupplyInventoryLots.AnyAsync(lot => lot.ExpiredDate > seedAnchorUtc));
        Assert.True(await context.AssemblyEvents.AnyAsync(assemblyEvent =>
            assemblyEvent.Status == "Gathering"
            && assemblyEvent.CheckInDeadline > seedAnchorUtc));
    }

    [Fact]
    public async Task DemoSeedValidator_FlagsInvalidEnumsClusterInventoryAndChatRules()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var validator = new DemoSeedValidator();
        var victimId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

        context.Users.Add(new User
        {
            Id = victimId,
            RoleId = 5,
            Username = "victim-invalid",
            Phone = "0900000000",
            Password = "hash"
        });
        context.SosClusters.AddRange(
            new SosCluster { Id = 9001, Status = "Done" },
            new SosCluster { Id = 9002, Status = "Completed" },
            new SosCluster { Id = 9003, Status = "InProgress" },
            new SosCluster { Id = 9004, Status = "Pending" });
        context.SosRequests.AddRange(
            new SosRequest
            {
                Id = 9001,
                ClusterId = 9001,
                UserId = victimId,
                SosType = "RESCUE",
                PriorityLevel = "Moderate",
                Status = "Completed",
                Location = new Point(107.6, 16.4) { SRID = 4326 }
            },
            new SosRequest
            {
                Id = 9002,
                ClusterId = 9002,
                UserId = victimId,
                SosType = "Rescue",
                PriorityLevel = "High",
                Status = "InProgress",
                Location = new Point(107.61, 16.41) { SRID = 4326 }
            },
            new SosRequest
            {
                Id = 9003,
                ClusterId = 9003,
                UserId = victimId,
                SosType = "Rescue",
                PriorityLevel = "High",
                Status = "Resolved",
                Location = new Point(107.62, 16.42) { SRID = 4326 }
            },
            new SosRequest
            {
                Id = 9004,
                ClusterId = 9004,
                UserId = victimId,
                SosType = "Rescue",
                PriorityLevel = "Critical",
                Status = "Pending",
                Location = new Point(107.63, 16.43) { SRID = 4326 }
            },
            new SosRequest
            {
                Id = 9005,
                ClusterId = 9004,
                UserId = victimId,
                SosType = "Rescue",
                PriorityLevel = "Critical",
                Status = "Pending",
                Location = new Point(107.631, 16.431) { SRID = 4326 }
            });
        context.Missions.Add(new Mission { Id = 9001, MissionType = "SUPPLY", Status = "Cancelled" });
        context.MissionActivities.Add(new MissionActivity { Id = 9001, MissionId = 9001, Status = "Done" });
        context.TeamIncidents.Add(new TeamIncident { Id = 9001, Status = "Acknowledged" });
        context.SupplyInventories.Add(new SupplyInventory
        {
            Id = 9001,
            Quantity = 5,
            MissionReservedQuantity = 6,
            TransferReservedQuantity = 0
        });
        context.SupplyInventoryLots.Add(new SupplyInventoryLot
        {
            Id = 9001,
            SupplyInventoryId = 9001,
            Quantity = 10,
            RemainingQuantity = 12,
            CreatedAt = DateTime.UtcNow
        });
        context.Conversations.Add(new Conversation
        {
            Id = 9001,
            VictimId = victimId,
            Status = "CoordinatorActive"
        });
        await context.SaveChangesAsync();

        var errors = await validator.ValidateAsync(context, CancellationToken.None);

        Assert.Contains(errors, error => error.Contains("sos_requests.priority_level", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sos_requests.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sos_requests.sos_type", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sos_clusters.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Completed SOS clusters contain non-resolved SOS requests", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("SOS clusters with only resolved requests must be Completed", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("SOS clusters exceed severity-based request limits", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missions.mission_type", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missions.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("mission_activities.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("team_incidents.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Inventory has", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Inventory lots", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missing their victim participant", StringComparison.Ordinal));
    }

    private static DatabaseSeeder CreateSeeder(ResQDbContext context, bool failOnValidationError = true)
    {
        var options = Options.Create(new SeedDataOptions
        {
            Profile = "Demo",
            AnchorDate = new DateOnly(2026, 4, 24),
            RandomSeed = 20260424,
            FailOnValidationError = failOnValidationError
        });

        return new DatabaseSeeder(
            context,
            options,
            new DemoSeedValidator(),
            NullLogger<DatabaseSeeder>.Instance);
    }

    private static DateTime SeedAnchorUtc() =>
        new DateTime(2026, 4, 24, 16, 59, 59, DateTimeKind.Utc).AddTicks(TimeSpan.TicksPerSecond - 1);

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static async Task<SeedCounts> CountsAsync(ResQDbContext context)
    {
        return new SeedCounts(
            await context.Users.CountAsync(),
            await context.SosRequests.CountAsync(),
            await context.SosClusters.CountAsync(),
            await context.Missions.CountAsync(),
            await context.MissionActivities.CountAsync(),
            await context.Conversations.CountAsync(),
            await context.Messages.CountAsync(),
            await context.SupplyInventories.CountAsync(),
            await context.DepotSupplyRequests.CountAsync(),
            await context.InventoryLogs.CountAsync());
    }

    private static async Task<List<SosCoordinateSnapshot>> LoadSosCoordinateSnapshotsAsync(ResQDbContext context)
    {
        return (await context.SosRequests
                .Where(s => s.Location != null)
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.ClusterId, Location = s.Location! })
                .ToListAsync())
            .Select(s => new SosCoordinateSnapshot(s.Id, s.ClusterId, Math.Round(s.Location.Y, 6), Math.Round(s.Location.X, 6)))
            .ToList();
    }

    private static bool IsCapsLockToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Any(char.IsLetter)
        && string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);

    private static string HighestSosPriority(IEnumerable<string?> priorities)
    {
        var prioritySet = priorities
            .Where(priority => !string.IsNullOrWhiteSpace(priority))
            .ToHashSet(StringComparer.Ordinal);

        if (prioritySet.Contains("Critical"))
        {
            return "Critical";
        }

        if (prioritySet.Contains("High"))
        {
            return "High";
        }

        if (prioritySet.Contains("Medium"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static int MaxSosRequestsForCluster(IEnumerable<string?> priorities) =>
        HighestSosPriority(priorities) switch
        {
            "Critical" => 1,
            "High" => 2,
            "Medium" => 3,
            _ => 5
        };

    private sealed record SeedCounts(
        int Users,
        int SosRequests,
        int SosClusters,
        int Missions,
        int MissionActivities,
        int Conversations,
        int Messages,
        int SupplyInventories,
        int SupplyRequests,
        int InventoryLogs);

    private sealed record SosCoordinateSnapshot(int Id, int? ClusterId, double Latitude, double Longitude);
}
