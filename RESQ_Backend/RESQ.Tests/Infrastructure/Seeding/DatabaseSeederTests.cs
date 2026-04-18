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
        Assert.Equal(274, firstCounts.Users);
        Assert.Equal(360, firstCounts.SosRequests);
        Assert.Equal(110, firstCounts.SosClusters);
        Assert.Equal(100, firstCounts.Missions);
        Assert.Equal(420, firstCounts.MissionActivities);
        Assert.Equal(140, firstCounts.Conversations);
        Assert.Equal(1900, firstCounts.Messages);
        Assert.Equal(620, firstCounts.SupplyInventories);
        Assert.Equal(95, firstCounts.SupplyRequests);
        Assert.Equal(820, firstCounts.InventoryLogs);
        Assert.Equal(1, await context.SystemMigrationAudits.CountAsync(a => a.MigrationName == "demo-seed-v1-2026-04-16"));
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

        Assert.Equal(120, await context.Users.CountAsync(u => u.RoleId == 3));
        Assert.Equal(10, unassignedRescuers.Count);
        foreach (var rescuer in unassignedRescuers)
        {
            Assert.Contains(rescuer.Id, eligibleRescuerIds);
        }

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

        var nowUtc = DateTime.UtcNow;
        var overdueOpenEvents = await context.AssemblyEvents
            .Where(e => e.CheckInDeadline <= nowUtc && e.Status != "Completed")
            .ToListAsync();
        Assert.Empty(overdueOpenEvents);

        var hueActiveEvent = await context.AssemblyEvents
            .SingleAsync(e => e.AssemblyPointId == hueStadium.Id && e.Status == "Gathering");
        Assert.True(hueActiveEvent.CheckInDeadline > nowUtc);

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
        Assert.Equal(10, hueCheckedInStandbyRescuers.Count);

        var depotHue = await context.Depots.SingleAsync(depot => depot.Name == "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế");
        var depotDaNang = await context.Depots.SingleAsync(depot => depot.Name == "Ủy ban MTTQVN TP Đà Nẵng");
        var depotHaTinh = await context.Depots.SingleAsync(depot => depot.Name == "Ủy Ban MTTQ Tỉnh Hà Tĩnh");
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg", depotHue.ImageUrl);
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg", depotDaNang.ImageUrl);
        Assert.Equal("https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg", depotHaTinh.ImageUrl);

        var unclusteredHueSos = await context.SosRequests
            .Where(s => s.ClusterId == null && s.Location != null)
            .Where(s => s.Location!.Y >= 16.455 && s.Location.Y <= 16.479)
            .Where(s => s.Location!.X >= 107.586 && s.Location.X <= 107.609)
            .ToListAsync();

        Assert.Equal(10, unclusteredHueSos.Count);
        Assert.DoesNotContain(await context.SosRequests.Select(s => s.PriorityLevel).Distinct().ToListAsync(), value => value == "Moderate");
        Assert.All(
            await context.SosRequests.Select(s => s.SosType).Distinct().ToListAsync(),
            sosType => Assert.False(IsCapsLockToken(sosType), $"Expected PascalCase sos_type but found '{sosType}'."));
        Assert.All(
            await context.Missions.Select(m => m.MissionType).Distinct().ToListAsync(),
            missionType => Assert.False(IsCapsLockToken(missionType), $"Expected PascalCase mission_type but found '{missionType}'."));
        Assert.All(
            await context.SosRequests.Where(s => s.StructuredData != null).Select(s => s.StructuredData!).Take(40).ToListAsync(),
            structuredData =>
            {
                using var document = JsonDocument.Parse(structuredData);
                var incidentSituation = document.RootElement.GetProperty("incident").GetProperty("situation").GetString();
                Assert.False(IsCapsLockToken(incidentSituation), $"Expected PascalCase situation but found '{incidentSituation}'.");

                foreach (var supply in document.RootElement.GetProperty("supplies").EnumerateArray().Select(element => element.GetString()))
                {
                    Assert.False(IsCapsLockToken(supply), $"Expected PascalCase supply but found '{supply}'.");
                }
            });
        Assert.All(
            await context.Users.Where(u => u.RoleId == 5).Select(u => u.Phone).ToListAsync(),
            phone => Assert.Matches("^\\+84[0-9]{9}$", phone ?? ""));
        Assert.All(
            await context.Users.Where(u => u.RoleId != 5).Select(u => u.Phone).ToListAsync(),
            phone => Assert.Matches("^0[0-9]{9}$", phone ?? ""));
        Assert.All(await context.SosRequests.Where(s => s.Location != null).Select(s => s.Location!).Take(40).ToListAsync(), point =>
        {
            Assert.InRange(point.Y, 14.9, 16.95);
            Assert.InRange(point.X, 106.9, 108.95);
        });
    }

    [Fact]
    public async Task DemoSeedValidator_FlagsInvalidEnumsInventoryAndChatRules()
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
        context.SosRequests.Add(new SosRequest
        {
            Id = 1,
            UserId = victimId,
            SosType = "RESCUE",
            PriorityLevel = "Moderate",
            Status = "Completed",
            Location = new Point(107.6, 16.4) { SRID = 4326 }
        });
        context.Missions.Add(new Mission { Id = 1, MissionType = "SUPPLY", Status = "Cancelled" });
        context.MissionActivities.Add(new MissionActivity { Id = 1, MissionId = 1, Status = "Done" });
        context.TeamIncidents.Add(new TeamIncident { Id = 1, Status = "Acknowledged" });
        context.SupplyInventories.Add(new SupplyInventory
        {
            Id = 1,
            Quantity = 5,
            MissionReservedQuantity = 6,
            TransferReservedQuantity = 0
        });
        context.SupplyInventoryLots.Add(new SupplyInventoryLot
        {
            Id = 1,
            SupplyInventoryId = 1,
            Quantity = 10,
            RemainingQuantity = 12,
            CreatedAt = DateTime.UtcNow
        });
        context.Conversations.Add(new Conversation
        {
            Id = 1,
            VictimId = victimId,
            Status = "CoordinatorActive"
        });
        await context.SaveChangesAsync();

        var errors = await validator.ValidateAsync(context, CancellationToken.None);

        Assert.Contains(errors, error => error.Contains("sos_requests.priority_level", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sos_requests.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sos_requests.sos_type", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missions.mission_type", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missions.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("mission_activities.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("team_incidents.status", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Inventory has", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Inventory lots", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missing their victim participant", StringComparison.Ordinal));
    }

    private static DatabaseSeeder CreateSeeder(ResQDbContext context)
    {
        var options = Options.Create(new SeedDataOptions
        {
            Profile = "Demo",
            AnchorDate = new DateOnly(2026, 4, 16),
            RandomSeed = 20260416,
            FailOnValidationError = true
        });

        return new DatabaseSeeder(
            context,
            options,
            new DemoSeedValidator(),
            NullLogger<DatabaseSeeder>.Instance);
    }

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

    private static bool IsCapsLockToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Any(char.IsLetter)
        && string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);

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
}
