using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Handle_FiltersByRepeatedStatuses_CaseInsensitive()
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
            Statuses = ["pending", "Assigned"]
        }, CancellationToken.None);

        Assert.Equal([2, 1], result.Select(x => x.Id).ToArray());
        Assert.Equal(
            [SosRequestStatus.Pending, SosRequestStatus.Assigned],
            repository.LastStatuses?.ToArray());
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
        DateTime createdAtUtc)
    {
        var sos = SosRequestModel.Create(
            UserId,
            new GeoLocation(latitude, longitude),
            $"SOS {id}",
            status: status,
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

        public Task<List<SosRequestModel>> GetByBoundsAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            IReadOnlyCollection<SosRequestStatus>? statuses = null,
            CancellationToken cancellationToken = default)
        {
            LastMinLat = minLat;
            LastMaxLat = maxLat;
            LastMinLng = minLng;
            LastMaxLng = maxLng;
            LastStatuses = statuses;

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

            return Task.FromResult(query
                .OrderByDescending(x => x.CreatedAt)
                .ToList());
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
