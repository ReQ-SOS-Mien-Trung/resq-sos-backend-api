using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
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
    public async Task Handle_ForwardsStatusesToRepository_AndReturnsFilteredPage()
    {
        var repository = new StubSosRequestRepository(
        [
            BuildSos(1, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(2, SosRequestStatus.Assigned, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(3, SosRequestStatus.Assigned, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc))
        ]);
        var handler = new GetSosRequestsPagedQueryHandler(
            repository,
            new StubSosRequestUpdateRepository(),
            NullLogger<GetSosRequestsPagedQueryHandler>.Instance);

        var result = await handler.Handle(new GetSosRequestsPagedQuery
        {
            PageNumber = 1,
            PageSize = 2,
            Statuses = [SosRequestStatus.Assigned]
        }, CancellationToken.None);

        Assert.Equal([SosRequestStatus.Assigned], repository.LastStatuses);
        Assert.Equal([2, 3], result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }

    private static SosRequestModel BuildSos(int id, SosRequestStatus status, DateTime createdAtUtc)
    {
        var sos = SosRequestModel.Create(
            UserId,
            new GeoLocation(10.75, 106.66),
            $"SOS {id}",
            status: status,
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

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SosRequestModel>>([]);
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SosRequestModel>>(requests);

        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            IReadOnlyCollection<SosRequestStatus>? statuses = null,
            CancellationToken cancellationToken = default)
        {
            LastStatuses = statuses;

            var query = requests.AsEnumerable();
            if (statuses is { Count: > 0 })
                query = query.Where(request => statuses.Contains(request.Status));

            var filtered = query
                .OrderByDescending(request => request.CreatedAt)
                .ToList();

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
