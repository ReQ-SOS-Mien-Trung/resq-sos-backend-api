using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Operations.Queries;

public class GetMissionTeamReportQueryHandlerTests
{
    [Fact]
    public async Task Handle_AllowsSystemUserViewerToReadReport_AndForcesReadOnlyFlags()
    {
        var leaderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memberId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var viewerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var mission = new MissionModel
        {
            Id = 7,
            Activities =
            [
                new MissionActivityModel
                {
                    Id = 101,
                    MissionId = 7,
                    MissionTeamId = 55,
                    Step = 1,
                    ActivityType = "RESCUE",
                    Status = MissionActivityStatus.OnGoing
                }
            ]
        };

        var missionTeam = new MissionTeamModel
        {
            Id = 55,
            MissionId = 7,
            RescuerTeamId = 20,
            Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo
                {
                    UserId = leaderId,
                    FullName = "Leader",
                    IsLeader = true,
                    Status = TeamMemberStatus.Accepted.ToString()
                },
                new MissionTeamMemberInfo
                {
                    UserId = memberId,
                    FullName = "Volunteer",
                    IsLeader = false,
                    Status = TeamMemberStatus.Accepted.ToString()
                }
            ]
        };

        var report = new MissionTeamReportModel
        {
            MissionTeamId = 55,
            ReportStatus = MissionTeamReportStatus.Draft,
            TeamSummary = "Mission summary",
            ActivityReports =
            [
                new MissionActivityReportModel
                {
                    MissionActivityId = 101,
                    ExecutionStatus = "ongoing",
                    Summary = "Reached site"
                }
            ],
            MemberEvaluations =
            [
                new MissionTeamMemberEvaluationModel
                {
                    RescuerId = memberId,
                    ResponseTimeScore = 8,
                    RescueEffectivenessScore = 8,
                    DecisionHandlingScore = 8,
                    SafetyMedicalSkillScore = 8,
                    TeamworkCommunicationScore = 8
                }
            ]
        };

        var handler = new GetMissionTeamReportQueryHandler(
            new StubMissionRepository(mission),
            new StubMissionTeamRepository(missionTeam),
            new StubMissionTeamReportRepository(report),
            new StubUserPermissionResolver([PermissionConstants.SystemUserView]));

        var result = await handler.Handle(new GetMissionTeamReportQuery(7, 55, viewerId), CancellationToken.None);

        Assert.Equal(7, result.MissionId);
        Assert.Equal(55, result.MissionTeamId);
        Assert.Equal(MissionTeamReportStatus.Draft.ToString(), result.ReportStatus);
        Assert.False(result.CanEdit);
        Assert.False(result.CanSubmit);
        Assert.False(result.CanEvaluateMembers);
        Assert.Single(result.Activities);
        Assert.Single(result.MemberEvaluations);
    }

    [Fact]
    public async Task Handle_WhenRequesterIsNotMemberAndHasNoSystemUserView_ThrowsForbidden()
    {
        var mission = new MissionModel
        {
            Id = 7,
            Activities =
            [
                new MissionActivityModel
                {
                    Id = 101,
                    MissionId = 7,
                    MissionTeamId = 55,
                    Step = 1,
                    ActivityType = "RESCUE",
                    Status = MissionActivityStatus.OnGoing
                }
            ]
        };

        var missionTeam = new MissionTeamModel
        {
            Id = 55,
            MissionId = 7,
            RescuerTeamId = 20,
            Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo
                {
                    UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    FullName = "Leader",
                    IsLeader = true,
                    Status = TeamMemberStatus.Accepted.ToString()
                }
            ]
        };

        var handler = new GetMissionTeamReportQueryHandler(
            new StubMissionRepository(mission),
            new StubMissionTeamRepository(missionTeam),
            new StubMissionTeamReportRepository(null),
            new StubUserPermissionResolver([]));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new GetMissionTeamReportQuery(7, 55, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
                CancellationToken.None));
    }

    private sealed class StubMissionRepository(MissionModel mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionModel?>(mission);

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(MissionModel model, Guid coordinatorId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(MissionModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubMissionTeamRepository(MissionTeamModel missionTeam) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionTeamModel?>(missionTeam);

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubMissionTeamReportRepository(MissionTeamReportModel? report) : IMissionTeamReportRepository
    {
        public Task<MissionTeamReportModel?> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(report);

        public Task<int> UpsertDraftAsync(MissionTeamReportModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SubmitAsync(int missionTeamId, Guid submittedBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateReportStatusAsync(int missionTeamId, MissionTeamReportStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubUserPermissionResolver(IReadOnlyCollection<string> permissionCodes) : IUserPermissionResolver
    {
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissionCodes);
    }
}
