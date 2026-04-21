using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Operations.Queries;

public class GetMissionActivitiesQueryHandlerTests
{
    [Fact]
    public async Task Handle_MapsImageUrl_FromMissionActivityModel()
    {
        const string imageUrl = "https://cdn.example.com/activity-proof.jpg";

        var activityRepository = new StubMissionActivityRepository(
        [
            new MissionActivityModel
            {
                Id = 1,
                MissionId = 7,
                Step = 2,
                ActivityType = "RESCUE",
                Description = "Test activity",
                ImageUrl = imageUrl
            }
        ]);

        var handler = new GetMissionActivitiesQueryHandler(
            activityRepository,
            new StubSosRequestRepository(),
            new StubSosRequestUpdateRepository(),
            new StubItemModelMetadataRepository(),
            NullLogger<GetMissionActivitiesQueryHandler>.Instance);

        var result = await handler.Handle(new GetMissionActivitiesQuery(7), CancellationToken.None);

        var activity = Assert.Single(result);
        Assert.Equal(imageUrl, activity.ImageUrl);
    }

    [Fact]
    public async Task Handle_EnrichesTargetVictims_FromStructuredData()
    {
        var activityRepository = new StubMissionActivityRepository(
        [
            new MissionActivityModel
            {
                Id = 1,
                MissionId = 7,
                Step = 2,
                ActivityType = "RESCUE",
                Description = "Tiếp cận hiện trường",
                SosRequestId = 99
            }
        ]);

        var sosRequestRepository = new StubSosRequestRepository(
            new SosRequestModel
            {
                Id = 99,
                StructuredData =
                    """
                    {
                      "incident": {
                        "people_count": {
                          "adult": 1,
                          "child": 1,
                          "elderly": 1
                        }
                      },
                      "victims": [
                        {
                          "person_id": "victim-1",
                          "person_type": "CHILD",
                          "index": 1,
                          "custom_name": "Khoa",
                          "person_phone": "+84972513978",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "SEVERE",
                            "medical_issues": ["FRACTURE", "BLEEDING"]
                          }
                        },
                        {
                          "person_id": "victim-2",
                          "person_type": "ADULT",
                          "index": 1,
                          "custom_name": "Thảo"
                        },
                        {
                          "person_id": "victim-3",
                          "person_type": "ELDERLY",
                          "index": 1,
                          "custom_name": "Chu"
                        }
                      ]
                    }
                    """
            });

        var handler = new GetMissionActivitiesQueryHandler(
            activityRepository,
            sosRequestRepository,
            new StubSosRequestUpdateRepository(),
            new StubItemModelMetadataRepository(),
            NullLogger<GetMissionActivitiesQueryHandler>.Instance);

        var result = await handler.Handle(new GetMissionActivitiesQuery(7), CancellationToken.None);

        var activity = Assert.Single(result);
        Assert.Equal("Khoa (trẻ em), Thảo (người lớn), Chu (người già)", activity.TargetVictimSummary);
        Assert.Equal(3, activity.TargetVictims.Count);
        Assert.Contains("Đối tượng cần hỗ trợ: Khoa (trẻ em), Thảo (người lớn), Chu (người già).", activity.Description);

        var khoa = Assert.Single(activity.TargetVictims, victim => victim.DisplayName == "Khoa");
        Assert.True(khoa.IsInjured);
        Assert.Equal("SEVERE", khoa.Severity);
        Assert.Contains("FRACTURE", khoa.MedicalIssues);
    }

    private sealed class StubMissionActivityRepository(IEnumerable<MissionActivityModel> activities) : IMissionActivityRepository
    {
        private readonly List<MissionActivityModel> _activities = activities.ToList();

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_activities.FirstOrDefault(activity => activity.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_activities.Where(activity => activity.MissionId == missionId).AsEnumerable());

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<MissionActivityModel>());

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int activityId, RESQ.Domain.Enum.Operations.MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestRepository(params SosRequestModel[] requests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _requests = requests.ToDictionary(request => request.Id);

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.GetValueOrDefault(id));

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusByClusterIdAsync(int clusterId, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubItemModelMetadataRepository : IItemModelMetadataRepository
    {
        public Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<int, ItemModelRecord>());
        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
