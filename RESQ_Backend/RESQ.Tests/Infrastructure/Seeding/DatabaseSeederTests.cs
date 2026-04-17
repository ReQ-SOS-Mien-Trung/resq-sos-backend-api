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
        Assert.Equal(260, firstCounts.Users);
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

        Assert.Equal(20, unassignedRescuers.Count);
        foreach (var rescuer in unassignedRescuers)
        {
            Assert.Contains(rescuer.Id, eligibleRescuerIds);
        }

        Assert.DoesNotContain(await context.SosRequests.Select(s => s.PriorityLevel).Distinct().ToListAsync(), value => value == "Moderate");
        Assert.All(await context.Users.Select(u => u.Phone).ToListAsync(), phone => Assert.Matches("^0[0-9]{9}$", phone ?? ""));
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
            PriorityLevel = "Moderate",
            Status = "Completed",
            Location = new Point(107.6, 16.4) { SRID = 4326 }
        });
        context.Missions.Add(new Mission { Id = 1, Status = "Cancelled" });
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
