using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class SubmitMissionTeamReportCommandHandlerTests
{
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-4444-4444-4444-444444444444");

    [Fact]
    public async Task Handle_FinalReportCompletesMission_SetsClusterStatusCompleted()
    {
        var mission = new MissionModel
        {
            Id = 12,
            ClusterId = 7,
            Status = MissionStatus.OnGoing,
            Activities =
            [
                new MissionActivityModel
                {
                    Id = 101,
                    MissionId = 12,
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
            MissionId = 12,
            RescuerTeamId = 21,
            Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
            ReportStatus = MissionTeamReportStatus.Draft.ToString(),
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo
                {
                    UserId = LeaderId,
                    IsLeader = true,
                    Status = TeamMemberStatus.Accepted.ToString()
                }
            ]
        };

        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.InProgress
        };

        var missionRepository = new StubMissionRepository(mission);
        var activityRepository = new StubMissionActivityRepository(mission);
        var missionTeamRepository = new StubMissionTeamRepository(missionTeam);
        var reportRepository = new StubMissionTeamReportRepository();
        var sosRequestRepository = new StubSosRequestRepository();
        var sosClusterRepository = new StubSosClusterRepository(cluster);
        var unitOfWork = new StubUnitOfWork();
        var lifecycleSyncService = new StubRescueTeamMissionLifecycleSyncService(
            new RescueTeamMissionLifecycleSyncResult([21]));

        var handler = new SubmitMissionTeamReportCommandHandler(
            missionRepository,
            activityRepository,
            missionTeamRepository,
            reportRepository,
            new StubRescuerScoreRepository(),
            sosRequestRepository,
            new StubSosRequestUpdateRepository(),
            sosClusterRepository,
            new StubTeamIncidentRepository(),
            lifecycleSyncService,
            unitOfWork,
            NullLogger<SubmitMissionTeamReportCommandHandler>.Instance);

        await handler.Handle(
            new SubmitMissionTeamReportCommand(
                MissionId: 12,
                MissionTeamId: 55,
                SubmittedBy: LeaderId,
                TeamSummary: "done",
                TeamNote: null,
                IssuesJson: null,
                ResultJson: null,
                EvidenceJson: null,
                Activities:
                [
                    new SubmitMissionTeamReportActivityItemDto
                    {
                        MissionActivityId = 101,
                        ExecutionStatus = "succeed",
                        Summary = "rescued"
                    }
                ],
                MemberEvaluations: []),
            CancellationToken.None);

        Assert.Equal(MissionStatus.Completed, missionRepository.LastStatus);
        Assert.True(missionRepository.LastIsCompleted);
        Assert.Equal(SosClusterStatus.Completed, sosClusterRepository.UpdatedCluster?.Status);
        Assert.Equal(SosRequestStatus.Resolved, sosRequestRepository.LastClusterStatus);
        Assert.Equal(MissionTeamExecutionStatus.Reported.ToString(), missionTeam.Status);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal((21, 55), lifecycleSyncService.LastExecutionSync!.Value);
        Assert.Equal(1, lifecycleSyncService.PushCalls);
    }

    private sealed class StubMissionRepository(MissionModel mission) : IMissionRepository
    {
        public MissionStatus? LastStatus { get; private set; }
        public bool? LastIsCompleted { get; private set; }

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionModel?>(mission);

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CreateAsync(MissionModel model, Guid coordinatorId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(MissionModel model, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default)
        {
            LastStatus = status;
            LastIsCompleted = isCompleted;
            mission.Status = status;
            mission.IsCompleted = isCompleted;
            return Task.CompletedTask;
        }
    }

    private sealed class StubMissionActivityRepository(MissionModel mission) : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(mission.Activities.FirstOrDefault(x => x.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<MissionActivityModel>>(mission.Activities);

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<MissionActivityModel>>([]);

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
        {
            var activity = mission.Activities.First(x => x.Id == activityId);
            activity.Status = status;
            activity.LastDecisionBy = decisionBy;
            return Task.CompletedTask;
        }

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubMissionTeamRepository(MissionTeamModel missionTeam) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionTeamModel?>(missionTeam);

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<MissionTeamModel>>([missionTeam]);

        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
        {
            missionTeam.Status = status;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default)
        {
            missionTeam.Status = status;
            missionTeam.Note = note;
            return Task.CompletedTask;
        }

        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default)
        {
            missionTeam.Latitude = latitude;
            missionTeam.Longitude = longitude;
            missionTeam.LocationSource = locationSource;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubMissionTeamReportRepository : IMissionTeamReportRepository
    {
        private MissionTeamReportModel? _report;

        public Task<MissionTeamReportModel?> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_report);

        public Task<int> UpsertDraftAsync(MissionTeamReportModel model, CancellationToken cancellationToken = default)
        {
            _report = model;
            _report.Id = 901;
            _report.StartedAt ??= DateTime.UtcNow;
            _report.LastEditedAt = DateTime.UtcNow;
            return Task.FromResult(_report.Id);
        }

        public Task SubmitAsync(int missionTeamId, Guid submittedBy, CancellationToken cancellationToken = default)
        {
            if (_report is not null)
            {
                _report.ReportStatus = MissionTeamReportStatus.Submitted;
                _report.SubmittedAt = DateTime.UtcNow;
                _report.SubmittedBy = submittedBy;
            }

            return Task.CompletedTask;
        }

        public Task UpdateReportStatusAsync(int missionTeamId, MissionTeamReportStatus status, CancellationToken cancellationToken = default)
        {
            if (_report is not null)
                _report.ReportStatus = status;

            return Task.CompletedTask;
        }
    }

    private sealed class StubRescuerScoreRepository : IRescuerScoreRepository
    {
        public Task<RescuerScoreModel?> GetByRescuerIdAsync(Guid rescuerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RescuerScoreModel?>(null);

        public Task<IDictionary<Guid, RescuerScoreModel>> GetByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDictionary<Guid, RescuerScoreModel>>(new Dictionary<Guid, RescuerScoreModel>());

        public Task<RescuerScoreModel?> GetVisibleByRescuerIdAsync(Guid rescuerId, int minimumEvaluationCount, CancellationToken cancellationToken = default) =>
            Task.FromResult<RescuerScoreModel?>(null);

        public Task<IDictionary<Guid, RescuerScoreModel>> GetVisibleByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, int minimumEvaluationCount, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDictionary<Guid, RescuerScoreModel>>(new Dictionary<Guid, RescuerScoreModel>());

        public Task RefreshAsync(IEnumerable<MissionTeamMemberEvaluationModel> newEvaluations, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubSosRequestRepository : ISosRequestRepository
    {
        public SosRequestStatus? LastClusterStatus { get; private set; }

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<SosRequestModel?>(null);

        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SosRequestModel>>([]);

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
        {
            LastClusterStatus = status;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubSosClusterRepository(SosClusterModel cluster) : ISosClusterRepository
    {
        public SosClusterModel? UpdatedCluster { get; private set; }

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<SosClusterModel?>(cluster);

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CreateAsync(SosClusterModel model, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(SosClusterModel model, CancellationToken cancellationToken = default)
        {
            UpdatedCluster = model;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubTeamIncidentRepository : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TeamIncidentModel?>(null);

        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubRescueTeamMissionLifecycleSyncService(
        RescueTeamMissionLifecycleSyncResult? result = null) : IRescueTeamMissionLifecycleSyncService
    {
        private readonly RescueTeamMissionLifecycleSyncResult _result = result ?? RescueTeamMissionLifecycleSyncResult.None;

        public (int RescueTeamId, int MissionTeamId)? LastExecutionSync { get; private set; }
        public int PushCalls { get; private set; }

        public Task<RescueTeamMissionLifecycleSyncResult> SyncTeamsToOnMissionAsync(IEnumerable<int> rescueTeamIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(RescueTeamMissionLifecycleSyncResult.None);

        public Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterReturnAsync(int rescueTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RescueTeamMissionLifecycleSyncResult.None);

        public Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterExecutionAsync(int rescueTeamId, int missionTeamId, CancellationToken cancellationToken = default)
        {
            LastExecutionSync = (rescueTeamId, missionTeamId);
            return Task.FromResult(_result);
        }

        public Task PushRealtimeIfNeededAsync(RescueTeamMissionLifecycleSyncResult result, CancellationToken cancellationToken = default)
        {
            if (result.HasChanges)
            {
                PushCalls++;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int ExecuteInTransactionCalls { get; private set; }

        public IGenericRepository<T> GetRepository<T>() where T : class =>
            throw new NotImplementedException();

        public IQueryable<T> Set<T>() where T : class =>
            throw new NotImplementedException();

        public IQueryable<T> SetTracked<T>() where T : class =>
            throw new NotImplementedException();

        public int SaveChangesWithTransaction() =>
            throw new NotImplementedException();

        public Task<int> SaveChangesWithTransactionAsync() =>
            throw new NotImplementedException();

        public Task<int> SaveAsync() =>
            Task.FromResult(1);

        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void ClearTrackedChanges()
        {
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            ExecuteInTransactionCalls++;
            await action();
        }
    }
}
