using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class MissionAiSuggestionRepository(IUnitOfWork unitOfWork) : IMissionAiSuggestionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
    {
        // 1. Save mission suggestion
        var missionEntity = MissionAiSuggestionMapper.ToEntity(model);
        await _unitOfWork.GetRepository<MissionAiSuggestion>().AddAsync(missionEntity);
        await _unitOfWork.SaveAsync();

        // 2. Save activity suggestions linked to mission
        if (model.Activities.Count > 0)
        {
            var activityRepo = _unitOfWork.GetRepository<ActivityAiSuggestion>();
            foreach (var activity in model.Activities)
            {
                activity.ParentMissionSuggestionId = missionEntity.Id;
                var activityEntity = MissionAiSuggestionMapper.ToActivityEntity(activity);
                await activityRepo.AddAsync(activityEntity);
            }
            await _unitOfWork.SaveAsync();
        }

        return missionEntity.Id;
    }

    public async Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionAiSuggestion>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        if (entity is null) return null;

        var activities = await _unitOfWork.GetRepository<ActivityAiSuggestion>()
            .GetAllByPropertyAsync(x => x.ParentMissionSuggestionId == id);

        return MissionAiSuggestionMapper.ToDomain(entity, activities);
    }

    public async Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var missions = await _unitOfWork.GetRepository<MissionAiSuggestion>()
            .GetAllByPropertyAsync(x => x.ClusterId == clusterId);

        if (missions.Count == 0) return [];

        var missionIds = missions.Select(m => m.Id).ToHashSet();
        var allActivities = await _unitOfWork.GetRepository<ActivityAiSuggestion>()
            .GetAllByPropertyAsync(x => x.ClusterId == clusterId && x.ParentMissionSuggestionId != null
                && missionIds.Contains(x.ParentMissionSuggestionId!.Value));

        var activitiesByMission = allActivities
            .GroupBy(a => a.ParentMissionSuggestionId!.Value)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        return missions.Select(m =>
            MissionAiSuggestionMapper.ToDomain(m,
                activitiesByMission.TryGetValue(m.Id, out var acts) ? acts : null));
    }
}
