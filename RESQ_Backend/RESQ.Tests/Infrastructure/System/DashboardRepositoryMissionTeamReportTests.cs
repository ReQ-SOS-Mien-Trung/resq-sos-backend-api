using Microsoft.EntityFrameworkCore;
using RESQ.Application.Extensions;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.System;

namespace RESQ.Tests.Infrastructure.System;

public class DashboardRepositoryMissionTeamReportTests
{
    [Fact]
    public async Task GetMissionTeamReportDashboardSummaryAsync_CountsNotStartedDraftAndSubmittedTeams()
    {
        await using var context = CreateContext();
        SeedMissionTeamReportDashboardData(context);
        await context.SaveChangesAsync();

        var repository = new DashboardRepository(context);

        var result = await repository.GetMissionTeamReportDashboardSummaryAsync();

        Assert.Equal(3, result.TotalCompletedTeams);
        Assert.Equal(1, result.NotStartedCount);
        Assert.Equal(1, result.DraftCount);
        Assert.Equal(1, result.SubmittedCount);
        Assert.Equal(33.33, result.SubmissionRate);
    }

    [Fact]
    public async Task GetMissionTeamReportDashboardSummaryAsync_AppliesReportStatusFilter()
    {
        await using var context = CreateContext();
        SeedMissionTeamReportDashboardData(context);
        await context.SaveChangesAsync();

        var repository = new DashboardRepository(context);

        var result = await repository.GetMissionTeamReportDashboardSummaryAsync(
            [MissionTeamReportStatus.Draft, MissionTeamReportStatus.Submitted]);

        Assert.Equal(2, result.TotalCompletedTeams);
        Assert.Equal(0, result.NotStartedCount);
        Assert.Equal(1, result.DraftCount);
        Assert.Equal(1, result.SubmittedCount);
        Assert.Equal(50, result.SubmissionRate);
    }

    [Fact]
    public async Task GetMissionTeamReportsDashboardAsync_ReturnsOnlyPostExecutionTeams_InExpectedSortOrder()
    {
        await using var context = CreateContext();
        SeedMissionTeamReportDashboardData(context);
        await context.SaveChangesAsync();

        var repository = new DashboardRepository(context);

        var result = await repository.GetMissionTeamReportsDashboardAsync(pageNumber: 1, pageSize: 10);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal([103, 102, 101], result.Items.Select(item => item.MissionTeamId).ToArray());

        var submitted = result.Items[0];
        Assert.Equal(MissionTeamReportStatus.Submitted.ToString(), submitted.ReportStatus);
        Assert.Equal(new DateTime(2026, 4, 22, 5, 0, 0, DateTimeKind.Utc).ToVietnamTime(), submitted.SubmittedAt);

        var draft = result.Items[1];
        Assert.Equal(MissionTeamReportStatus.Draft.ToString(), draft.ReportStatus);
        Assert.Equal(new DateTime(2026, 4, 22, 4, 0, 0, DateTimeKind.Utc).ToVietnamTime(), draft.LastEditedAt);

        var notStarted = result.Items[2];
        Assert.Equal(MissionTeamReportStatus.NotStarted.ToString(), notStarted.ReportStatus);
        Assert.Null(notStarted.LastEditedAt);
        Assert.Null(notStarted.SubmittedAt);
    }

    [Fact]
    public async Task GetMissionTeamReportsDashboardAsync_AppliesReportStatusTeamIdAndSearchFilters()
    {
        await using var context = CreateContext();
        SeedMissionTeamReportDashboardData(context);
        await context.SaveChangesAsync();

        var repository = new DashboardRepository(context);

        var result = await repository.GetMissionTeamReportsDashboardAsync(
            pageNumber: 1,
            pageSize: 10,
            reportStatus: MissionTeamReportStatus.Draft.ToString(),
            teamId: 20,
            search: "alpha");

        var item = Assert.Single(result.Items);
        Assert.Equal(102, item.MissionTeamId);
        Assert.Equal("TEAM-ALPHA", item.TeamCode);
        Assert.Equal("Alpha Team", item.TeamName);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetMissionTeamReportsDashboardAsync_AppliesPaginationAfterSorting()
    {
        await using var context = CreateContext();
        SeedMissionTeamReportDashboardData(context);
        await context.SaveChangesAsync();

        var repository = new DashboardRepository(context);

        var result = await repository.GetMissionTeamReportsDashboardAsync(pageNumber: 2, pageSize: 1);

        var item = Assert.Single(result.Items);
        Assert.Equal(102, item.MissionTeamId);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
    }

    private static void SeedMissionTeamReportDashboardData(ResQDbContext context)
    {
        context.AssemblyPoints.AddRange(
            new AssemblyPoint { Id = 1, Code = "AP-1", Name = "Assembly One" },
            new AssemblyPoint { Id = 2, Code = "AP-2", Name = "Assembly Two" });

        context.RescueTeams.AddRange(
            new RescueTeam { Id = 10, Code = "TEAM-BETA", Name = "Beta Team", AssemblyPointId = 2, Status = "Available" },
            new RescueTeam { Id = 20, Code = "TEAM-ALPHA", Name = "Alpha Team", AssemblyPointId = 1, Status = "Available" },
            new RescueTeam { Id = 30, Code = "TEAM-GAMMA", Name = "Gamma Team", AssemblyPointId = 1, Status = "Available" },
            new RescueTeam { Id = 40, Code = "TEAM-DELTA", Name = "Delta Team", AssemblyPointId = 2, Status = "Available" });

        context.Missions.AddRange(
            new Mission { Id = 1, MissionType = "RESCUE", Status = "OnGoing" },
            new Mission { Id = 2, MissionType = "SUPPLY", Status = "Completed" },
            new Mission { Id = 3, MissionType = "EVACUATION", Status = "OnGoing" },
            new Mission { Id = 4, MissionType = "RESCUE", Status = "OnGoing" });

        context.MissionTeams.AddRange(
            new MissionTeam
            {
                Id = 101,
                MissionId = 1,
                RescuerTeamId = 10,
                Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
                AssignedAt = new DateTime(2026, 4, 22, 2, 0, 0, DateTimeKind.Utc)
            },
            new MissionTeam
            {
                Id = 102,
                MissionId = 2,
                RescuerTeamId = 20,
                Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
                AssignedAt = new DateTime(2026, 4, 22, 3, 0, 0, DateTimeKind.Utc)
            },
            new MissionTeam
            {
                Id = 103,
                MissionId = 3,
                RescuerTeamId = 30,
                Status = MissionTeamExecutionStatus.Reported.ToString(),
                AssignedAt = new DateTime(2026, 4, 22, 1, 0, 0, DateTimeKind.Utc)
            },
            new MissionTeam
            {
                Id = 104,
                MissionId = 4,
                RescuerTeamId = 40,
                Status = MissionTeamExecutionStatus.InProgress.ToString(),
                AssignedAt = new DateTime(2026, 4, 22, 6, 0, 0, DateTimeKind.Utc)
            });

        context.MissionTeamReports.AddRange(
            new MissionTeamReport
            {
                Id = 1002,
                MissionTeamId = 102,
                ReportStatus = MissionTeamReportStatus.Draft.ToString(),
                LastEditedAt = new DateTime(2026, 4, 22, 4, 0, 0, DateTimeKind.Utc)
            },
            new MissionTeamReport
            {
                Id = 1003,
                MissionTeamId = 103,
                ReportStatus = MissionTeamReportStatus.Submitted.ToString(),
                LastEditedAt = new DateTime(2026, 4, 22, 4, 30, 0, DateTimeKind.Utc),
                SubmittedAt = new DateTime(2026, 4, 22, 5, 0, 0, DateTimeKind.Utc)
            },
            new MissionTeamReport
            {
                Id = 1004,
                MissionTeamId = 104,
                ReportStatus = MissionTeamReportStatus.Submitted.ToString(),
                SubmittedAt = new DateTime(2026, 4, 22, 7, 0, 0, DateTimeKind.Utc)
            });
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }
}
