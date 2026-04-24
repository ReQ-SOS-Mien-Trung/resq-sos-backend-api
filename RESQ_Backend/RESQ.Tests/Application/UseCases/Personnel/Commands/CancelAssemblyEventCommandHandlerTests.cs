using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Personnel.Commands.CancelAssemblyEvent;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Personnel.Commands;

public class CancelAssemblyEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_CancelsGatheringEvent_AndNotifiesParticipants()
    {
        var participantIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var assemblyEventRepository = new StubAssemblyEventRepository(
            eventSnapshot: (10, 5, AssemblyEventStatus.Gathering.ToString(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            participantIds: participantIds);
        var assemblyPointRepository = new StubAssemblyPointRepository(new AssemblyPointModel
        {
            Id = 5,
            Name = "AP-05",
            Status = AssemblyPointStatus.Available
        });
        var firebaseService = new RecordingFirebaseService();
        var unitOfWork = new StubUnitOfWork();

        var handler = new CancelAssemblyEventCommandHandler(
            assemblyEventRepository,
            assemblyPointRepository,
            unitOfWork,
            firebaseService,
            NullLogger<CancelAssemblyEventCommandHandler>.Instance);

        var result = await handler.Handle(new CancelAssemblyEventCommand(10, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(AssemblyEventStatus.Cancelled.ToString(), result.Status);
        Assert.Equal(AssemblyEventStatus.Cancelled.ToString(), assemblyEventRepository.UpdatedStatus);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(2, firebaseService.Notifications.Count);
    }

    [Fact]
    public async Task Handle_Throws_WhenEventIsNotGathering()
    {
        var handler = new CancelAssemblyEventCommandHandler(
            new StubAssemblyEventRepository(
                eventSnapshot: (10, 5, AssemblyEventStatus.Completed.ToString(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
                participantIds: []),
            new StubAssemblyPointRepository(new AssemblyPointModel
            {
                Id = 5,
                Name = "AP-05",
                Status = AssemblyPointStatus.Available
            }),
            new StubUnitOfWork(),
            new RecordingFirebaseService(),
            NullLogger<CancelAssemblyEventCommandHandler>.Instance);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new CancelAssemblyEventCommand(10, Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("Gathering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubAssemblyEventRepository(
        (int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)? eventSnapshot,
        List<Guid> participantIds) : IAssemblyEventRepository
    {
        public string? UpdatedStatus { get; private set; }

        public Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(eventSnapshot);

        public Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default)
        {
            UpdatedStatus = status;
            return Task.CompletedTask;
        }

        public Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(participantIds);

        public Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, DateTime checkInDeadline, Guid createdBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutVoluntaryAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ReturnCheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers.CheckedInRescuerDto>> GetCheckedInRescuersAsync(int eventId, int pageNumber, int pageSize, RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents.AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int EventId, string Status)?> GetLatestEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetEventCreatedByAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasParticipantCheckedOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> MarkParticipantAbsentAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents.MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents.UpcomingAssemblyEventDto>> GetUpcomingEventsForRescuerAsync(Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsWithExpiredDeadlineAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsExpiredAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CompleteEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> AutoMarkAbsentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubAssemblyPointRepository(AssemblyPointModel? assemblyPoint) : IAssemblyPointRepository
    {
        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(assemblyPoint);

        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default, string? statusFilter = null) => throw new NotImplementedException();
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class RecordingFirebaseService : IFirebaseService
    {
        public List<(Guid UserId, string Title, string Body, string Type)> Notifications { get; } = [];

        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default)
        {
            Notifications.Add((userId, title, body, type));
            return Task.CompletedTask;
        }

        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
}
