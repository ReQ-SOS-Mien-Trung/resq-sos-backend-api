using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestStatusCounts;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosRequestStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllStatuses_AndIgnoresUnknownStatusCounts()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 25, 23, 59, 59, DateTimeKind.Utc);
        var repository = new StubSosRequestStatisticsRepository(new Dictionary<string, int>
        {
            [SosRequestStatus.Pending.ToString()] = 50,
            [SosRequestStatus.Assigned.ToString()] = 60,
            ["Unknown"] = 5
        });
        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new GetSosRequestStatusCountsQuery(from, to),
            CancellationToken.None);

        Assert.Equal(from, result.From);
        Assert.Equal(to, result.To);
        Assert.Equal(110, result.Total);
        Assert.Equal(
            Enum.GetValues<SosRequestStatus>().Select(status => status.ToString()).ToArray(),
            result.StatusCounts.Select(item => item.Status).ToArray());
        Assert.Equal(50, CountFor(result, SosRequestStatus.Pending));
        Assert.Equal(60, CountFor(result, SosRequestStatus.Assigned));
        Assert.Equal(0, CountFor(result, SosRequestStatus.InProgress));
        Assert.DoesNotContain(result.StatusCounts, item => item.Status == "Unknown");
    }

    [Fact]
    public async Task Handle_DefaultsToLastSixMonths_WhenRangeIsMissing()
    {
        var repository = new StubSosRequestStatisticsRepository(new Dictionary<string, int>());
        var handler = CreateHandler(repository);
        var before = DateTime.UtcNow;

        var result = await handler.Handle(
            new GetSosRequestStatusCountsQuery(null, null),
            CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.InRange(result.To, before, after);
        Assert.Equal(result.To.AddMonths(-6), result.From);
        Assert.Equal(result.From, repository.LastFrom);
        Assert.Equal(result.To, repository.LastTo);
    }

    [Fact]
    public async Task Handle_SwapsRange_WhenFromIsAfterTo()
    {
        var from = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var repository = new StubSosRequestStatisticsRepository(new Dictionary<string, int>());
        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new GetSosRequestStatusCountsQuery(from, to),
            CancellationToken.None);

        Assert.Equal(to, result.From);
        Assert.Equal(from, result.To);
        Assert.Equal(to, repository.LastFrom);
        Assert.Equal(from, repository.LastTo);
    }

    private static GetSosRequestStatusCountsQueryHandler CreateHandler(
        ISosRequestStatisticsRepository repository)
        => new(repository, NullLogger<GetSosRequestStatusCountsQueryHandler>.Instance);

    private static int CountFor(
        GetSosRequestStatusCountsResponse response,
        SosRequestStatus status)
        => response.StatusCounts.Single(item => item.Status == status.ToString()).Count;

    private sealed class StubSosRequestStatisticsRepository(
        IReadOnlyDictionary<string, int> counts) : ISosRequestStatisticsRepository
    {
        public DateTime? LastFrom { get; private set; }
        public DateTime? LastTo { get; private set; }

        public Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            LastFrom = from;
            LastTo = to;
            return Task.FromResult(counts);
        }
    }
}
