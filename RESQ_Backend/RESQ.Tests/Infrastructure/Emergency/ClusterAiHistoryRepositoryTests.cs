using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Emergency;

namespace RESQ.Tests.Infrastructure.Emergency;

public class ClusterAiHistoryRepositoryTests
{
    [Fact]
    public async Task DeleteByClusterIdAsync_RemovesAllClusterScopedAiHistory()
    {
        await using var context = CreateContext();

        context.SosClusters.Add(new SosCluster
        {
            Id = 7,
            Status = "Suggested",
            CreatedAt = DateTime.UtcNow
        });

        context.MissionAiSuggestions.AddRange(
            new MissionAiSuggestion
            {
                Id = 1001,
                ClusterId = 7,
                AnalysisType = "MissionPlanning",
                CreatedAt = DateTime.UtcNow
            },
            new MissionAiSuggestion
            {
                Id = 1002,
                ClusterId = 99,
                AnalysisType = "MissionPlanning",
                CreatedAt = DateTime.UtcNow
            });

        context.ActivityAiSuggestions.AddRange(
            new ActivityAiSuggestion
            {
                Id = 2001,
                ClusterId = 7,
                ParentMissionSuggestionId = 1001,
                ActivityType = "RESCUE",
                SuggestionPhase = "Draft",
                CreatedAt = DateTime.UtcNow
            },
            new ActivityAiSuggestion
            {
                Id = 2002,
                ClusterId = 99,
                ParentMissionSuggestionId = 1002,
                ActivityType = "RESCUE",
                SuggestionPhase = "Draft",
                CreatedAt = DateTime.UtcNow
            });

        context.ClusterAiAnalyses.AddRange(
            new ClusterAiAnalysis
            {
                Id = 3001,
                ClusterId = 7,
                AnalysisType = "Severity",
                CreatedAt = DateTime.UtcNow
            },
            new ClusterAiAnalysis
            {
                Id = 3002,
                ClusterId = 99,
                AnalysisType = "Severity",
                CreatedAt = DateTime.UtcNow
            });

        context.RescueTeamAiSuggestions.AddRange(
            new RescueTeamAiSuggestion
            {
                Id = 4001,
                ClusterId = 7,
                AnalysisType = "TeamPlanning",
                CreatedAt = DateTime.UtcNow
            },
            new RescueTeamAiSuggestion
            {
                Id = 4002,
                ClusterId = 99,
                AnalysisType = "TeamPlanning",
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        await repository.DeleteByClusterIdAsync(7);
        await context.SaveChangesAsync();

        Assert.Null(await context.MissionAiSuggestions.FindAsync(1001));
        Assert.Null(await context.ActivityAiSuggestions.FindAsync(2001));
        Assert.Null(await context.ClusterAiAnalyses.FindAsync(3001));
        Assert.Null(await context.RescueTeamAiSuggestions.FindAsync(4001));

        Assert.NotNull(await context.MissionAiSuggestions.FindAsync(1002));
        Assert.NotNull(await context.ActivityAiSuggestions.FindAsync(2002));
        Assert.NotNull(await context.ClusterAiAnalyses.FindAsync(3002));
        Assert.NotNull(await context.RescueTeamAiSuggestions.FindAsync(4002));
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static ClusterAiHistoryRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new ClusterAiHistoryRepository(unitOfWork);
    }
}
