using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;
using RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Personnel.Commands;

public class CreateRescueTeamCommandHandlerTests
{
    [Fact]
    public async Task Handle_AllowsCoordinatorToCreateTeam_FromCheckedInMembersUsingLatestEvent()
    {
        var leaderId = Guid.NewGuid();
        var memberIds = new[]
        {
            leaderId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };

        var assemblyPointRepository = new StubAssemblyPointRepository(new AssemblyPointModel
        {
            Id = 10,
            Name = "AP-01",
            Status = AssemblyPointStatus.Available
        });
        var assemblyEventRepository = new StubAssemblyEventRepository(
            latestEvent: (501, AssemblyEventStatus.Completed.ToString()),
            checkedInUsers: memberIds.ToHashSet());
        var userRepository = new StubUserRepository(memberIds.ToDictionary(
            id => id,
            id => new UserModel
            {
                Id = id,
                RoleId = 3,
                FirstName = "Test",
                LastName = "Rescuer",
                RescuerType = id == leaderId ? RescuerType.Core : RescuerType.Volunteer
            }));
        var teamRepository = new StubRescueTeamRepository();
        var unitOfWork = new StubUnitOfWork();

        var handler = new CreateRescueTeamCommandHandler(
            teamRepository,
            assemblyPointRepository,
            assemblyEventRepository,
            userRepository,
            new NoOpAdminRealtimeHubService(),
            new NoOpFirebaseService(),
            unitOfWork,
            NullLogger<CreateRescueTeamCommandHandler>.Instance);

        var command = new CreateRescueTeamCommand(
            "Team Alpha",
            RescueTeamType.Rescue,
            10,
            Guid.NewGuid(),
            6,
            memberIds.Select((id, index) => new AddMemberRequestDto
            {
                UserId = id,
                IsLeader = index == 0
            }).ToList());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result);
        Assert.Equal(501, assemblyEventRepository.LastCheckedEventId);
        Assert.Equal(memberIds.Length, assemblyPointRepository.BulkUpdatedUserIds.Count);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_Throws_WhenAssemblyPointHasNoAssemblyEvent()
    {
        var memberId = Guid.NewGuid();

        var handler = new CreateRescueTeamCommandHandler(
            new StubRescueTeamRepository(),
            new StubAssemblyPointRepository(new AssemblyPointModel
            {
                Id = 10,
                Name = "AP-01",
                Status = AssemblyPointStatus.Available
            }),
            new StubAssemblyEventRepository(latestEvent: null, checkedInUsers: [memberId]),
            new StubUserRepository(new Dictionary<Guid, UserModel>
            {
                [memberId] = new()
                {
                    Id = memberId,
                    RoleId = 3,
                    FirstName = "Lead",
                    LastName = "Rescuer",
                    RescuerType = RescuerType.Core
                }
            }),
            new NoOpAdminRealtimeHubService(),
            new NoOpFirebaseService(),
            new StubUnitOfWork(),
            NullLogger<CreateRescueTeamCommandHandler>.Instance);

        var command = new CreateRescueTeamCommand(
            "Team Beta",
            RescueTeamType.Mixed,
            10,
            Guid.NewGuid(),
            6,
            Enumerable.Range(0, 6)
                .Select(index => new AddMemberRequestDto
                {
                    UserId = index == 0 ? memberId : Guid.NewGuid(),
                    IsLeader = index == 0
                })
                .ToList());

        var ex = await Assert.ThrowsAsync<RESQ.Application.Exceptions.BadRequestException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Contains("chưa có sự kiện tập trung nào", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubAssemblyPointRepository(AssemblyPointModel? assemblyPoint) : IAssemblyPointRepository
    {
        public List<Guid> BulkUpdatedUserIds { get; } = [];

        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(assemblyPoint);

        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default)
        {
            BulkUpdatedUserIds.AddRange(userIds);
            return Task.FromResult(userIds.ToList());
        }

        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default, string? statusFilter = null) => throw new NotImplementedException();
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubAssemblyEventRepository(
        (int EventId, string Status)? latestEvent,
        HashSet<Guid> checkedInUsers) : IAssemblyEventRepository
    {
        public int? LastCheckedEventId { get; private set; }

        public Task<(int EventId, string Status)?> GetLatestEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult(latestEvent);

        public Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default)
        {
            LastCheckedEventId = eventId;
            return Task.FromResult(checkedInUsers.Contains(rescuerId));
        }

        public Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult(((int EventId, string Status)?)null);

        public Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, DateTime checkInDeadline, Guid createdBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutVoluntaryAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ReturnCheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers.CheckedInRescuerDto>> GetCheckedInRescuersAsync(int eventId, int pageNumber, int pageSize, RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents.AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetEventCreatedByAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasParticipantCheckedOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> MarkParticipantAbsentAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents.MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents.UpcomingAssemblyEventDto>> GetUpcomingEventsForRescuerAsync(Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsWithExpiredDeadlineAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsExpiredAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CompleteEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> AutoMarkAbsentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubUserRepository(Dictionary<Guid, UserModel> users) : IUserRepository
    {
        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(users.TryGetValue(id, out var user) ? user : null);

        public Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(UserModel user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, RescuerType? rescuerType = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? name = null, string? phone = null, string? email = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubRescueTeamRepository : IRescueTeamRepository
    {
        private RescueTeamModel? _createdTeam;

        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
        {
            team.SetId(1);
            _createdTeam = team;
            return Task.CompletedTask;
        }

        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(_createdTeam);

        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("RESCUE");

        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveAsync()
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => throw new NotImplementedException();
        public Task<int> SaveChangesWithTransactionAsync() => throw new NotImplementedException();
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class => throw new NotImplementedException();
        public Task ExecuteInTransactionAsync(Func<Task> action) => throw new NotImplementedException();
    }

    private sealed class NoOpAdminRealtimeHubService : IAdminRealtimeHubService
    {
        public Task PushFundingRequestUpdateAsync(RESQ.Application.Common.Models.AdminFundingRequestRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushCampaignUpdateAsync(RESQ.Application.Common.Models.AdminCampaignRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDisbursementUpdateAsync(RESQ.Application.Common.Models.AdminDisbursementRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushRescuerApplicationUpdateAsync(RESQ.Application.Common.Models.AdminRescuerApplicationRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDepotUpdateAsync(RESQ.Application.Common.Models.AdminDepotRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDepotClosureUpdateAsync(RESQ.Application.Common.Models.AdminDepotClosureRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushTransferUpdateAsync(RESQ.Application.Common.Models.AdminTransferRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushSOSClusterUpdateAsync(RESQ.Application.Common.Models.AdminSOSClusterRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushMissionUpdateAsync(RESQ.Application.Common.Models.AdminMissionRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushMissionActivityUpdateAsync(RESQ.Application.Common.Models.AdminMissionActivityRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushRescueTeamUpdateAsync(RESQ.Application.Common.Models.AdminRescueTeamRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushSystemConfigUpdateAsync(RESQ.Application.Common.Models.AdminSystemConfigRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushAiConfigUpdateAsync(RESQ.Application.Common.Models.AdminAiConfigRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpFirebaseService : IFirebaseService
    {
        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
