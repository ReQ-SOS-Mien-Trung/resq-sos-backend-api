using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

/// <summary>
/// FE-04 – Spatial Data &amp; Infrastructure Logic: GetSosClusters query handler tests.
/// Covers: Geographical Clustering (DBSCAN), cluster retrieval, severity levels.
/// </summary>
public class GetSosClustersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoClustersExist()
    {
        var handler = BuildHandler([]);

        var result = await handler.Handle(new GetSosClustersQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Clusters);
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

        var dto = Assert.Single(result.Clusters);
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

        var dto = Assert.Single(result.Clusters);
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
    public async Task Handle_ReturnsMultipleClusters_SortedById()
    {
        var clusters = new List<SosClusterModel>
        {
            new() { Id = 1, SeverityLevel = "Low", SosRequestIds = [1] },
            new() { Id = 2, SeverityLevel = "High", SosRequestIds = [2, 3] },
            new() { Id = 3, SeverityLevel = "Critical", SosRequestIds = [4, 5, 6] }
        };
        var handler = BuildHandler(clusters);

        var result = await handler.Handle(new GetSosClustersQuery(), CancellationToken.None);

        Assert.Equal(3, result.Clusters.Count);
    }

    // ── Builder ──

    private static GetSosClustersQueryHandler BuildHandler(List<SosClusterModel> clusters)
        => new(new StubClusterRepo(clusters), NullLogger<GetSosClustersQueryHandler>.Instance);

    // ── Stub ──

    private sealed class StubClusterRepo(List<SosClusterModel> clusters) : ISosClusterRepository
    {
        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosClusterModel>>(clusters);
        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(clusters.FirstOrDefault(c => c.Id == id));
        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken ct = default) => Task.FromResult(cluster.Id);
        public Task UpdateAsync(SosClusterModel cluster, CancellationToken ct = default) => Task.CompletedTask;
    }
}
