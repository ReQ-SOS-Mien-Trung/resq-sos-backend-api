using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Sorting;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosRequestsByBoundsQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task Handle_ReturnsOnlySosRequestsInsideBounds_OrderedByCreatedAtDescending()
    {
        var repository = new StubSosRequestMapReadRepository(
        [
            BuildSos(1, 10.75, 106.66, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(2, 10.78, 106.68, SosRequestStatus.Assigned, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(3, 11.50, 107.20, SosRequestStatus.Pending, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc))
        ]);
        var handler = BuildHandler(repository);

        var result = await handler.Handle(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70
        }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([2, 1], result.Select(x => x.Id).ToArray());
        Assert.Equal(10.70, repository.LastMinLat);
        Assert.Equal(10.80, repository.LastMaxLat);
        Assert.Equal(106.60, repository.LastMinLng);
        Assert.Equal(106.70, repository.LastMaxLng);
    }

    [Fact]
    public async Task Handle_FiltersByMultipleStatuses()
    {
        var repository = new StubSosRequestMapReadRepository(
        [
            BuildSos(1, 10.75, 106.66, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(2, 10.76, 106.67, SosRequestStatus.Assigned, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)),
            BuildSos(3, 10.77, 106.68, SosRequestStatus.Resolved, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc))
        ]);
        var handler = BuildHandler(repository);

        var result = await handler.Handle(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70,
            Statuses = [SosRequestStatus.Pending, SosRequestStatus.Assigned]
        }, CancellationToken.None);

        Assert.Equal([2, 1], result.Select(x => x.Id).ToArray());
        Assert.Equal(
            [SosRequestStatus.Pending, SosRequestStatus.Assigned],
            repository.LastStatuses?.ToArray());
    }

    [Fact]
    public async Task Handle_FiltersByPriorityAndSosType()
    {
        var repository = new StubSosRequestMapReadRepository(
        [
            BuildSos(1, 10.75, 106.66, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.High, sosType: SosRequestType.Rescue),
            BuildSos(2, 10.76, 106.67, SosRequestStatus.Pending, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.Critical, sosType: SosRequestType.Relief),
            BuildSos(3, 10.77, 106.68, SosRequestStatus.Pending, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), priorityLevel: SosPriorityLevel.High, sosType: SosRequestType.Relief)
        ]);
        var handler = BuildHandler(repository);

        var result = await handler.Handle(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70,
            SortOptions =
            [
                new SosSortOption(SosSortField.Severity, SosSortDirection.Desc),
                new SosSortOption(SosSortField.Time, SosSortDirection.Desc)
            ],
            Priorities = [SosPriorityLevel.High, SosPriorityLevel.High],
            SosTypes = [SosRequestType.Relief, SosRequestType.Relief]
        }, CancellationToken.None);

        Assert.Equal([3], result.Select(x => x.Id).ToArray());
        Assert.Equal([SosPriorityLevel.High], repository.LastPriorities?.ToArray());
        Assert.Equal([SosRequestType.Relief], repository.LastSosTypes?.ToArray());
        Assert.Equal(
            [new SosSortOption(SosSortField.Severity, SosSortDirection.Desc), new SosSortOption(SosSortField.Time, SosSortDirection.Desc)],
            repository.LastSortOptions);
    }

    [Fact]
    public async Task Handle_AppliesLatestVictimUpdate_AndLatestIncidentNote()
    {
        var sos = BuildSos(1, 10.75, 106.66, SosRequestStatus.Pending, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var repository = new StubSosRequestMapReadRepository([sos]);
        var updateRepository = new StubSosRequestUpdateRepository(
            victimUpdates: new Dictionary<int, SosRequestVictimUpdateModel>
            {
                [1] = new()
                {
                    Id = 11,
                    SosRequestId = 1,
                    RawMessage = "Updated SOS message",
                    SosType = "MEDICAL",
                    UpdatedByUserId = UserId,
                    UpdatedAt = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)
                }
            },
            incidentHistory: new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>
            {
                [1] =
                [
                    new SosRequestIncidentUpdateModel
                    {
                        Id = 21,
                        SosRequestId = 1,
                        Note = "Victim moved to rooftop",
                        CreatedAt = new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            });
        var handler = BuildHandler(repository, updateRepository);

        var result = await handler.Handle(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70
        }, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Updated SOS message", dto.RawMessage);
        Assert.Equal("MEDICAL", dto.SosType);
        Assert.Equal("Victim moved to rooftop", dto.LatestIncidentNote);
    }

    private static SosRequestModel BuildSos(
        int id,
        double latitude,
        double longitude,
        SosRequestStatus status,
        DateTime createdAtUtc,
        SosPriorityLevel? priorityLevel = null,
        SosRequestType? sosType = null)
    {
        var sos = SosRequestModel.Create(
            UserId,
            new GeoLocation(latitude, longitude),
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

    private static GetSosRequestsByBoundsQueryHandler BuildHandler(
        StubSosRequestMapReadRepository repository,
        StubSosRequestUpdateRepository? updateRepository = null)
    {
        return new GetSosRequestsByBoundsQueryHandler(
            repository,
            updateRepository ?? new StubSosRequestUpdateRepository(),
            NullLogger<GetSosRequestsByBoundsQueryHandler>.Instance);
    }

    private sealed class StubSosRequestMapReadRepository(List<SosRequestModel> requests) : ISosRequestMapReadRepository
    {
        public double LastMinLat { get; private set; }
        public double LastMaxLat { get; private set; }
        public double LastMinLng { get; private set; }
        public double LastMaxLng { get; private set; }
        public IReadOnlyCollection<SosRequestStatus>? LastStatuses { get; private set; }
        public IReadOnlyCollection<SosPriorityLevel>? LastPriorities { get; private set; }
        public IReadOnlyCollection<SosRequestType>? LastSosTypes { get; private set; }
        public IReadOnlyList<SosSortOption>? LastSortOptions { get; private set; }

        public Task<List<SosRequestModel>> GetByBoundsAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            IReadOnlyCollection<SosRequestStatus>? statuses = null,
            IReadOnlyCollection<SosPriorityLevel>? priorities = null,
            IReadOnlyCollection<SosRequestType>? sosTypes = null,
            IReadOnlyList<SosSortOption>? sortOptions = null,
            CancellationToken cancellationToken = default)
        {
            LastMinLat = minLat;
            LastMaxLat = maxLat;
            LastMinLng = minLng;
            LastMaxLng = maxLng;
            LastStatuses = statuses;
            LastPriorities = priorities;
            LastSosTypes = sosTypes;
            LastSortOptions = sortOptions;

            var query = requests
                .Where(x => x.Location != null
                    && x.Location.Latitude >= minLat
                    && x.Location.Latitude <= maxLat
                    && x.Location.Longitude >= minLng
                    && x.Location.Longitude <= maxLng);

            if (statuses is { Count: > 0 })
            {
                query = query.Where(x => statuses.Contains(x.Status));
            }

            if (priorities is { Count: > 0 })
            {
                query = query.Where(x => x.PriorityLevel.HasValue && priorities.Contains(x.PriorityLevel.Value));
            }

            if (sosTypes is { Count: > 0 })
            {
                query = query.Where(x => !string.IsNullOrWhiteSpace(x.SosType) && sosTypes.Select(sosType => sosType.ToString()).Contains(x.SosType));
            }

            return Task.FromResult(SosSortParser.ApplyToRequests(query, sortOptions).ToList());
        }
    }

    private sealed class StubSosRequestUpdateRepository(
        Dictionary<int, SosRequestVictimUpdateModel>? victimUpdates = null,
        Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>? incidentHistory = null)
        : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(
            IEnumerable<int> teamIncidentIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(
                new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(
                new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(
                victimUpdates ?? new Dictionary<int, SosRequestVictimUpdateModel>());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(
                incidentHistory ?? new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }
}
