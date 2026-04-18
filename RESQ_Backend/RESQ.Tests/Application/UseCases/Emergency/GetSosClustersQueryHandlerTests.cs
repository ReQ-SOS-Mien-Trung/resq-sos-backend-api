using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosClustersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyPagedResult_WhenNoClustersExist()
    {
        var handler = BuildHandler([]);

        var result = await handler.Handle(new GetSosClustersQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_ReturnsClusters_WithCorrectSosRequestCount()
    {
        var cluster = new SosClusterModel
        {
            Id = 1,
            CenterLatitude = 16.047079,
            CenterLongitude = 108.206230,
            RadiusKm = 5.0,
            SeverityLevel = "High",
            VictimEstimated = 25,
            SosRequestIds = [101, 102, 103]
        };
        var handler = BuildHandler([cluster]);

        var result = await handler.Handle(new GetSosClustersQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal(1, dto.Id);
        Assert.Equal(3, dto.SosRequestCount);
        Assert.Equal(new List<int> { 101, 102, 103 }, dto.SosRequestIds);
    }

    [Fact]
    public async Task Handle_MapsAllFields_FromClusterModel()
    {
        var cluster = new SosClusterModel
        {
            Id = 10,
            CenterLatitude = 16.0,
            CenterLongitude = 108.0,
            RadiusKm = 3.5,
            SeverityLevel = "Critical",
            WaterLevel = "1.5m",
            VictimEstimated = 50,
            ChildrenCount = 10,
            ElderlyCount = 8,
            MedicalUrgencyScore = 85.5,
            Status = SosClusterStatus.InProgress,
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            SosRequestIds = [1, 2]
        };
        var handler = BuildHandler([cluster]);

        var result = await handler.Handle(new GetSosClustersQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal(16.0, dto.CenterLatitude);
        Assert.Equal(108.0, dto.CenterLongitude);
        Assert.Equal(3.5, dto.RadiusKm);
        Assert.Equal("Critical", dto.SeverityLevel);
        Assert.Equal("1.5m", dto.WaterLevel);
        Assert.Equal(50, dto.VictimEstimated);
        Assert.Equal(10, dto.ChildrenCount);
        Assert.Equal(8, dto.ElderlyCount);
        Assert.Equal(85.5, dto.MedicalUrgencyScore);
        Assert.Equal(SosClusterStatus.InProgress, dto.Status);
    }

    [Fact]
    public async Task Handle_FiltersBySosRequestId()
    {
        var handler = BuildHandler(
        [
            new SosClusterModel { Id = 1, SosRequestIds = [10, 11] },
            new SosClusterModel { Id = 2, SosRequestIds = [12, 13] }
        ]);

        var result = await handler.Handle(new GetSosClustersQuery(1, 10, 12), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal(2, dto.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_NormalizesInvalidPaging_AndReturnsRequestedPage()
    {
        var clusters = new List<SosClusterModel>
        {
            new() { Id = 1, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), SosRequestIds = [1] },
            new() { Id = 2, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), SosRequestIds = [2] },
            new() { Id = 3, CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc), SosRequestIds = [3] }
        };
        var handler = BuildHandler(clusters);

        var normalized = await handler.Handle(new GetSosClustersQuery(0, 0), CancellationToken.None);
        var paged = await handler.Handle(new GetSosClustersQuery(2, 1), CancellationToken.None);

        Assert.Equal(1, normalized.PageNumber);
        Assert.Equal(10, normalized.PageSize);
        Assert.Equal(3, normalized.TotalCount);

        var dto = Assert.Single(paged.Items);
        Assert.Equal(2, dto.Id);
        Assert.Equal(2, paged.PageNumber);
        Assert.Equal(1, paged.PageSize);
        Assert.Equal(3, paged.TotalCount);
    }

    private static GetSosClustersQueryHandler BuildHandler(List<SosClusterModel> clusters)
        => new(new StubClusterRepo(clusters), NullLogger<GetSosClustersQueryHandler>.Instance);

    private sealed class StubClusterRepo(List<SosClusterModel> clusters) : ISosClusterRepository
    {
        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosClusterModel>>(clusters);

        public Task<PagedResult<SosClusterModel>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            int? sosRequestId = null,
            CancellationToken cancellationToken = default)
        {
            var filtered = clusters
                .Where(cluster => !sosRequestId.HasValue || cluster.SosRequestIds.Contains(sosRequestId.Value))
                .OrderByDescending(cluster => cluster.CreatedAt)
                .ThenByDescending(cluster => cluster.Id)
                .ToList();

            var items = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<SosClusterModel>(items, filtered.Count, pageNumber, pageSize));
        }

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(clusters.FirstOrDefault(c => c.Id == id));

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken ct = default)
            => Task.FromResult(cluster.Id);

        public Task UpdateAsync(SosClusterModel cluster, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
