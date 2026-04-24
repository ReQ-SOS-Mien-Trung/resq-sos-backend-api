using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Services;

namespace RESQ.Tests.Infrastructure.Services;

public class MissionContextServiceTests
{
    [Fact]
    public void ExtractNeededSupplies_InfersTransportationAndRescueEquipmentForFloodIsolationEvacuation()
    {
        var method = typeof(MissionContextService).GetMethod(
            "ExtractNeededSupplies",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 1,
                RawMessage = "Khu vuc ngap sau, bi co lap, can ca no de so tan nguoi mac ket.",
                StructuredData = """{"group_needs":{"water":{"duration":"2 ngay"}}}"""
            }
        };

        var needed = (HashSet<string>)method!.Invoke(null, [sosRequests])!;

        Assert.Contains("WATER", needed);
        Assert.Contains("TRANSPORTATION", needed);
        Assert.Contains("RESCUE_EQUIPMENT", needed);
    }

    [Fact]
    public void ExtractNeededSupplies_ToleratesNullGroupNeedsAndReadsIncidentVictimPayload()
    {
        var method = typeof(MissionContextService).GetMethod(
            "ExtractNeededSupplies",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 362,
                SosType = "RESCUE",
                RawMessage = "[CUU HO] Tinh trang: Mac ket. Bi thuong: gay xuong. Ba gia Chu dang bi mat nhiet.",
                StructuredData =
                    """
                    {
                      "group_needs": null,
                      "incident": {
                        "additional_description": "Ba gia Chu dang bi mat nhiet. Can cuu gap!",
                        "need_medical": true,
                        "has_injured": true,
                        "situation": "TRAPPED"
                      },
                      "victims": [
                        {
                          "incident_status": {
                            "is_injured": true,
                            "medical_issues": ["FRACTURE"],
                            "severity": "MODERATE"
                          },
                          "personal_needs": {
                            "clothing": { "needed": false },
                            "diet": { "has_special_diet": false }
                          }
                        }
                      ]
                    }
                    """
            }
        };

        var needed = (HashSet<string>)method!.Invoke(null, [sosRequests])!;

        Assert.Contains("MEDICINE", needed);
        Assert.Contains("RESCUE_EQUIPMENT", needed);
        Assert.Contains("CLOTHING", needed);
    }

    [Fact]
    public async Task PrepareContextAsync_MapsLatestAiAnalysisIntoSosSummary()
    {
        var cluster = new SosClusterModel
        {
            Id = 42,
            Status = SosClusterStatus.Pending,
            SosRequestIds = [1001]
        };

        var sosRequest = new SosRequestModel
        {
            Id = 1001,
            ClusterId = 42,
            SosType = "BOTH",
            RawMessage = "Nuoc dang len nhanh, can di doi gap.",
            StructuredData = """{"group_needs":{"water":{"duration":"1 ngay"}}}""",
            Location = new GeoLocation(10.7769, 106.7009),
            PriorityLevel = SosPriorityLevel.High,
            Status = SosRequestStatus.Pending,
            CreatedAt = new DateTime(2026, 4, 23, 8, 0, 0, DateTimeKind.Utc)
        };

        var analysis = new SosAiAnalysisModel
        {
            SosRequestId = 1001,
            SuggestedPriority = "High",
            SuggestedSeverityLevel = "Severe",
            Explanation = "Phat hien nguy co tang nhanh.",
            Metadata =
                """
                {
                  "analysisResult": {
                    "suggested_priority": "Critical",
                    "suggested_severity": "Critical",
                    "needs_immediate_safe_transfer": true,
                    "can_wait_for_combined_mission": false,
                    "handling_reason": "Can dua nan nhan den diem an toan truoc."
                  }
                }
                """,
            CreatedAt = new DateTime(2026, 4, 23, 8, 5, 0, DateTimeKind.Utc)
        };

        var service = new MissionContextService(
            new StubSosClusterRepository(cluster),
            new StubSosRequestRepository(sosRequest),
            new StubSosRequestUpdateRepository(),
            new StubSosAiAnalysisRepository(analysis),
            new StubDepotRepository(),
            new StubPersonnelQueryRepository(),
            new StubRescueTeamRadiusConfigRepository(),
            NullLogger<MissionContextService>.Instance);

        var context = await service.PrepareContextAsync(cluster.Id);

        var summary = Assert.Single(context.SosRequests);
        var aiSummary = Assert.IsType<SosRequestAiAnalysisSummary>(summary.AiAnalysis);

        Assert.True(aiSummary.HasAiAnalysis);
        Assert.Equal("Critical", aiSummary.SuggestedPriority);
        Assert.Equal("Critical", aiSummary.SuggestedSeverity);
        Assert.True(aiSummary.NeedsImmediateSafeTransfer);
        Assert.False(aiSummary.CanWaitForCombinedMission);
        Assert.Equal("Can dua nan nhan den diem an toan truoc.", aiSummary.HandlingReason);
        Assert.Equal(analysis.CreatedAt, aiSummary.CreatedAt);
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

    private sealed class StubSosRequestRepository(params SosRequestModel[] sosRequests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _sosRequests = sosRequests.ToDictionary(request => request.Id);

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());

        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>(_sosRequests.Values);

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_sosRequests.GetValueOrDefault(id));

        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>(
                _sosRequests.Values.Where(request => request.ClusterId == clusterId).ToList());

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
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
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(
                new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubSosAiAnalysisRepository(params SosAiAnalysisModel[] analyses) : ISosAiAnalysisRepository
    {
        private readonly Dictionary<int, SosAiAnalysisModel> _analyses = analyses.ToDictionary(analysis => analysis.SosRequestId);

        public Task CreateAsync(SosAiAnalysisModel analysis, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_analyses.GetValueOrDefault(sosRequestId));

        public Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(
            int sosRequestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosAiAnalysisModel>>(
                _analyses.TryGetValue(sosRequestId, out var analysis)
                    ? [analysis]
                    : []);

        public Task<IReadOnlyDictionary<int, SosAiAnalysisModel>> GetLatestBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
        {
            var lookup = sosRequestIds
                .Distinct()
                .Where(_analyses.ContainsKey)
                .ToDictionary(id => id, id => _analyses[id]);

            return Task.FromResult<IReadOnlyDictionary<int, SosAiAnalysisModel>>(lookup);
        }
    }

    private sealed class StubDepotRepository : IDepotRepository
    {
        public Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AssignManagerAsync(DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UnassignSpecificManagersAsync(DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<DepotModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            IEnumerable<DepotStatus>? statuses = null,
            string? search = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<DepotModel>([], 0, pageNumber, pageSize));

        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<DepotModel>());

        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<DepotModel>());

        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<DepotModel?>(null);

        public Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<DepotModel?>(null);

        public Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(
            int depotId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(
            int depotId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(decimal PendingInboundVolume, decimal PendingInboundWeight)> GetPendingInboundLoadAsync(
            int depotId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((0m, 0m));

        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ManagedDepotDto>());

        public Task<List<DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DepotManagerInfoDto>());
    }

    private sealed class StubPersonnelQueryRepository : IPersonnelQueryRepository
    {
        public Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(
            int pageNumber,
            int pageSize,
            string? firstName = null,
            string? lastName = null,
            string? phone = null,
            string? email = null,
            RescuerType? rescuerType = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<RescueTeamModel?>(null);

        public Task<PagedResult<FreeRescuerModel>> GetRescuersByAssemblyPointAsync(
            int assemblyPointId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RescueTeamModel>> GetAllAvailableTeamsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RescueTeamModel>());

        public Task<PagedResult<RescuerModel>> GetRescuersAsync(
            int pageNumber,
            int pageSize,
            bool? hasAssemblyPoint = null,
            bool? hasTeam = null,
            RescuerType? rescuerType = null,
            string? abilitySubgroupCode = null,
            string? abilityCategoryCode = null,
            string? search = null,
            List<string>? assemblyPointCodes = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubRescueTeamRadiusConfigRepository : IRescueTeamRadiusConfigRepository
    {
        public Task<RescueTeamRadiusConfigDto?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<RescueTeamRadiusConfigDto?>(null);

        public Task<RescueTeamRadiusConfigDto> UpsertAsync(
            double maxRadiusKm,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
