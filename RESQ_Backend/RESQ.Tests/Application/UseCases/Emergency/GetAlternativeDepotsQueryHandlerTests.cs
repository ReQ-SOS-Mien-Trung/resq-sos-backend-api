using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetAlternativeDepotsQueryHandlerTests
{
    private static readonly JsonSerializerOptions SnakeCaseJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public async Task Handle_ReturnsRankedTopThreeAlternativeDepots()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel
            {
                Id = 7,
                CenterLatitude = 10.0,
                CenterLongitude = 106.0
            },
            suggestions:
            [
                CreateSuggestion(
                    id: 100,
                    createdAt: new DateTime(2026, 4, 11, 9, 0, 0, DateTimeKind.Utc),
                    shortages:
                    [
                        new SupplyShortageDto
                        {
                            ItemId = 1,
                            ItemName = "Nuoc sach",
                            Unit = "chai",
                            SelectedDepotId = 5,
                            MissingQuantity = 60
                        },
                        new SupplyShortageDto
                        {
                            ItemId = 1,
                            ItemName = "Nuoc sach",
                            Unit = "chai",
                            SelectedDepotId = 5,
                            MissingQuantity = 40
                        },
                        new SupplyShortageDto
                        {
                            ItemName = "Thuoc y te",
                            Unit = "hop",
                            SelectedDepotId = 5,
                            MissingQuantity = 20
                        }
                    ])
            ],
            depots:
            [
                CreateDepot(
                    id: 5,
                    latitude: 10.001,
                    longitude: 106.001,
                    inventoryLines:
                    [
                        new DepotInventoryLine(1, "Nuoc sach", "chai", 500)
                    ]),
                CreateDepot(
                    id: 12,
                    latitude: 10.01,
                    longitude: 106.01,
                    inventoryLines:
                    [
                        new DepotInventoryLine(1, "Nuoc sach", "chai", 100),
                        new DepotInventoryLine(9, "Thuốc y tế", "hop", 20)
                    ]),
                CreateDepot(
                    id: 11,
                    latitude: 10.03,
                    longitude: 106.03,
                    inventoryLines:
                    [
                        new DepotInventoryLine(1, "Nuoc sach", "chai", 110),
                        new DepotInventoryLine(8, "Thuoc y te", "hop", 25)
                    ]),
                CreateDepot(
                    id: 13,
                    latitude: 10.005,
                    longitude: 106.005,
                    inventoryLines:
                    [
                        new DepotInventoryLine(1, "Nuoc sach", "chai", 100)
                    ]),
                CreateDepot(
                    id: 14,
                    latitude: 10.02,
                    longitude: 106.02,
                    inventoryLines:
                    [
                        new DepotInventoryLine(999, "Nuoc sach", "chai", 999),
                        new DepotInventoryLine(10, "Thuoc y te", "hop", 20)
                    ]),
                CreateDepot(
                    id: 15,
                    latitude: 10.025,
                    longitude: 106.025,
                    inventoryLines:
                    [
                        new DepotInventoryLine(1, "Nuoc sach", "chai", 80),
                        new DepotInventoryLine(6, "Thuoc y te", "hop", 10)
                    ])
            ]);

        var response = await handler.Handle(new GetAlternativeDepotsQuery(7, 5), CancellationToken.None);

        Assert.Equal(7, response.ClusterId);
        Assert.Equal(5, response.SelectedDepotId);
        Assert.Equal(100, response.SourceSuggestionId);
        Assert.Equal(2, response.TotalShortageItems);
        Assert.Equal(120, response.TotalMissingQuantity);

        Assert.Collection(
            response.AlternativeDepots,
            depot =>
            {
                Assert.Equal(12, depot.DepotId);
                Assert.True(depot.CoversAllShortages);
                Assert.Equal(120, depot.CoveredQuantity);
                Assert.Equal(1, depot.CoveragePercent);
                Assert.Contains("120/120", depot.Reason);

                var water = Assert.Single(depot.ItemCoverageDetails, item => item.ItemId == 1);
                Assert.Equal(100, water.NeededQuantity);
                Assert.Equal(100, water.AvailableQuantity);
                Assert.Equal("Full", water.CoverageStatus);

                var medicine = Assert.Single(depot.ItemCoverageDetails, item => item.ItemId is null);
                Assert.Equal("Thuoc y te", medicine.ItemName);
                Assert.Equal(20, medicine.AvailableQuantity);
                Assert.Equal("Full", medicine.CoverageStatus);
            },
            depot =>
            {
                Assert.Equal(11, depot.DepotId);
                Assert.True(depot.CoversAllShortages);
                Assert.Equal(120, depot.CoveredQuantity);
            },
            depot =>
            {
                Assert.Equal(13, depot.DepotId);
                Assert.False(depot.CoversAllShortages);
                Assert.Equal(100, depot.CoveredQuantity);
                Assert.Contains("100/120", depot.Reason);
            });
    }

    [Fact]
    public async Task Handle_ReturnsEmptyResponse_WhenLatestSuggestionHasNoShortage()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel
            {
                Id = 3,
                CenterLatitude = 10.0,
                CenterLongitude = 106.0
            },
            suggestions:
            [
                CreateSuggestion(
                    id: 77,
                    createdAt: DateTime.UtcNow,
                    shortages: [])
            ],
            depots:
            [
                CreateDepot(10, 10.01, 106.01, [new DepotInventoryLine(1, "Nuoc sach", "chai", 50)])
            ]);

        var response = await handler.Handle(new GetAlternativeDepotsQuery(3, 9), CancellationToken.None);

        Assert.Equal(77, response.SourceSuggestionId);
        Assert.Equal(0, response.TotalShortageItems);
        Assert.Equal(0, response.TotalMissingQuantity);
        Assert.Empty(response.AlternativeDepots);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSelectedDepotDoesNotMatchShortageSource()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel
            {
                Id = 8,
                CenterLatitude = 10.0,
                CenterLongitude = 106.0
            },
            suggestions:
            [
                CreateSuggestion(
                    id: 55,
                    createdAt: DateTime.UtcNow,
                    shortages:
                    [
                        new SupplyShortageDto
                        {
                            ItemId = 1,
                            ItemName = "Nuoc sach",
                            SelectedDepotId = 99,
                            MissingQuantity = 10
                        }
                    ])
            ],
            depots: []);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new GetAlternativeDepotsQuery(8, 5), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenClusterDoesNotExist()
    {
        var handler = BuildHandler(cluster: null, suggestions: [], depots: []);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAlternativeDepotsQuery(1, 5), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenClusterHasNoCenterCoordinates()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel { Id = 9 },
            suggestions: [],
            depots: []);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new GetAlternativeDepotsQuery(9, 5), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenClusterHasNoMissionSuggestions()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel
            {
                Id = 11,
                CenterLatitude = 10.0,
                CenterLongitude = 106.0
            },
            suggestions: [],
            depots: []);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAlternativeDepotsQuery(11, 5), CancellationToken.None));
    }

    private static GetAlternativeDepotsQueryHandler BuildHandler(
        SosClusterModel? cluster,
        IEnumerable<MissionAiSuggestionModel> suggestions,
        IEnumerable<DepotModel> depots)
    {
        return new GetAlternativeDepotsQueryHandler(
            new StubSosClusterRepository(cluster),
            new StubMissionAiSuggestionRepository(suggestions),
            new StubDepotRepository(depots),
            NullLogger<GetAlternativeDepotsQueryHandler>.Instance);
    }

    private static MissionAiSuggestionModel CreateSuggestion(
        int id,
        DateTime? createdAt,
        List<SupplyShortageDto> shortages)
    {
        return new MissionAiSuggestionModel
        {
            Id = id,
            CreatedAt = createdAt,
            Metadata = JsonSerializer.Serialize(new
            {
                overall_assessment = "test",
                needs_additional_depot = shortages.Count > 0,
                supply_shortages = shortages
            }, SnakeCaseJsonOpts)
        };
    }

    private static DepotModel CreateDepot(
        int id,
        double latitude,
        double longitude,
        IEnumerable<DepotInventoryLine> inventoryLines)
    {
        var depot = new DepotModel
        {
            Id = id,
            Name = $"Kho {id}",
            Address = $"Dia chi {id}",
            Location = new GeoLocation(latitude, longitude),
            Status = DepotStatus.Available
        };
        depot.SetInventoryLines(inventoryLines);
        return depot;
    }

    private sealed class StubSosClusterRepository(SosClusterModel? cluster) : ISosClusterRepository
    {
        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(cluster?.Id == id ? cluster : null);

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosClusterModel>());

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubMissionAiSuggestionRepository(IEnumerable<MissionAiSuggestionModel> suggestions)
        : IMissionAiSuggestionRepository
    {
        private readonly List<MissionAiSuggestionModel> _suggestions = suggestions.ToList();

        public Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SavePipelineSnapshotAsync(int suggestionId, MissionSuggestionMetadata metadata, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_suggestions.FirstOrDefault(suggestion => suggestion.Id == id));

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_suggestions.AsEnumerable());

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdsAsync(IEnumerable<int> clusterIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionAiSuggestionModel>());
    }

    private sealed class StubDepotRepository(IEnumerable<DepotModel> depots) : IDepotRepository
    {
        private readonly List<DepotModel> _depots = depots.ToList();

        public Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignManagerAsync(DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignSpecificManagersAsync(DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RESQ.Application.Common.Models.PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_depots.AsEnumerable());
        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_depots.AsEnumerable());
        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(_depots.FirstOrDefault(depot => depot.Id == id));
        public Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_depots.FirstOrDefault(depot => depot.Name == name));
        public Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>());
    }
}
