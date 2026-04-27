using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Sorting;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosRequestsPagedQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task Handle_ForwardsFiltersToRepository_AndReturnsFilteredPage()
    {
        var repository = new StubSosRequestRepository(
        [
            BuildSos(1, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.Low, sosType: SosRequestType.Rescue),
            BuildSos(2, SosRequestStatus.Assigned, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.High, sosType: SosRequestType.Rescue),
            BuildSos(3, SosRequestStatus.Assigned, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.High, sosType: SosRequestType.Relief)
        ]);
        var handler = new GetSosRequestsPagedQueryHandler(
            repository,
            new StubSosRequestUpdateRepository(),
            NullLogger<GetSosRequestsPagedQueryHandler>.Instance);

        var result = await handler.Handle(new GetSosRequestsPagedQuery
        {
            PageNumber = 1,
            PageSize = 2,
            SosRequestId = 2,
            Statuses = [SosRequestStatus.Assigned],
            Priorities = [SosPriorityLevel.High, SosPriorityLevel.High],
            SosTypes = [SosRequestType.Rescue, SosRequestType.Rescue],
            SortOptions =
            [
                new SosSortOption(SosSortField.Severity, SosSortDirection.Desc),
                new SosSortOption(SosSortField.Time, SosSortDirection.Desc)
            ]
        }, CancellationToken.None);

        Assert.Equal(2, repository.LastSosRequestId);
        Assert.Equal([SosRequestStatus.Assigned], repository.LastStatuses);
        Assert.Equal([SosPriorityLevel.High], repository.LastPriorities);
        Assert.Equal([SosRequestType.Rescue], repository.LastSosTypes);
        Assert.Equal(
            [new SosSortOption(SosSortField.Severity, SosSortDirection.Desc), new SosSortOption(SosSortField.Time, SosSortDirection.Desc)],
            repository.LastSortOptions);
        Assert.Equal([2], result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }

    private static SosRequestModel BuildSos(
        int id,
        SosRequestStatus status,
        DateTime createdAtUtc,
        SosPriorityLevel? priorityLevel = null,
        SosRequestType? sosType = null)
    {
        var sos = SosRequestModel.Create(
            UserId,
            new GeoLocation(10.75, 106.66),
            $"SOS {id}",
            sosType: sosType?.ToString(),
            status: status,
            priorityLevel: priorityLevel,
            clientCreatedAt: createdAtUtc);

        sos.Id = id;
        sos.CreatedAt = createdAtUtc;
        sos.ReceivedAt = createdAtUtc;
        sos.LastUpdatedAt = createdAtUtc;
        return sos;
    }

    private sealed class StubSosRequestRepository(List<SosRequestModel> requests) : ISosRequestRepository
    {
        public IReadOnlyCollection<SosRequestStatus>? LastStatuses { get; private set; }
        public IReadOnlyCollection<SosPriorityLevel>? LastPriorities { get; private set; }
        public IReadOnlyCollection<SosRequestType>? LastSosTypes { get; private set; }
        public IReadOnlyList<SosSortOption>? LastSortOptions { get; private set; }
        public int? LastSosRequestId { get; private set; }

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SosRequestModel>>([]);
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SosRequestModel>>(requests);

        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            IReadOnlyCollection<SosRequestStatus>? statuses = null,
            IReadOnlyCollection<SosPriorityLevel>? priorities = null,
            IReadOnlyCollection<SosRequestType>? sosTypes = null,
            IReadOnlyList<SosSortOption>? sortOptions = null,
            int? sosRequestId = null,
            CancellationToken cancellationToken = default)
        {
            LastStatuses = statuses;
            LastPriorities = priorities;
            LastSosTypes = sosTypes;
            LastSortOptions = sortOptions;
            LastSosRequestId = sosRequestId;

            var query = requests.AsEnumerable();
            if (sosRequestId.HasValue)
                query = query.Where(request => sosRequestId.Value > 0 && request.Id == sosRequestId.Value);
            if (statuses is { Count: > 0 })
                query = query.Where(request => statuses.Contains(request.Status));
            if (priorities is { Count: > 0 })
                query = query.Where(request => request.PriorityLevel.HasValue && priorities.Contains(request.PriorityLevel.Value));
            if (sosTypes is { Count: > 0 })
                query = query.Where(request => !string.IsNullOrWhiteSpace(request.SosType) && sosTypes.Select(sosType => sosType.ToString()).Contains(request.SosType));

            var filtered = SosSortParser.ApplyToRequests(query, sortOptions).ToList();

            var items = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<SosRequestModel>(items, filtered.Count, pageNumber, pageSize));
        }

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<SosRequestModel?>(requests.FirstOrDefault(request => request.Id == id));

        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>([]);

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SosRequestModel>>([]);
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(
            IEnumerable<int> teamIncidentIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }
}
