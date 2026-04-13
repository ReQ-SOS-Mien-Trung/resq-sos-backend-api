using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;
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
            new StubItemModelMetadataRepository(),
            NullLogger<GetMissionActivitiesQueryHandler>.Instance);

        var result = await handler.Handle(new GetMissionActivitiesQuery(7), CancellationToken.None);

        var activity = Assert.Single(result);
        Assert.Equal(imageUrl, activity.ImageUrl);
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
