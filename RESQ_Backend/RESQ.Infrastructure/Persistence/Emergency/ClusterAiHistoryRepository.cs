using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class ClusterAiHistoryRepository(IUnitOfWork unitOfWork) : IClusterAiHistoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task DeleteByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var missionSuggestionIds = await _unitOfWork.Set<ActivityAiSuggestion>()
            .Where(activity => activity.ClusterId == clusterId && activity.ParentMissionSuggestionId.HasValue)
            .Select(activity => activity.ParentMissionSuggestionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missionIdsFromMissionTable = await _unitOfWork.Set<MissionAiSuggestion>()
            .Where(suggestion => suggestion.ClusterId == clusterId)
            .Select(suggestion => suggestion.Id)
            .ToListAsync(cancellationToken);

        missionSuggestionIds.AddRange(missionIdsFromMissionTable);
        var allMissionSuggestionIds = missionSuggestionIds.Distinct().ToList();

        var activitySuggestionIds = await _unitOfWork.Set<ActivityAiSuggestion>()
            .Where(activity => activity.ClusterId == clusterId
                || (activity.ParentMissionSuggestionId.HasValue
                    && allMissionSuggestionIds.Contains(activity.ParentMissionSuggestionId.Value)))
            .Select(activity => activity.Id)
            .ToListAsync(cancellationToken);

        foreach (var activitySuggestionId in activitySuggestionIds)
        {
            await _unitOfWork.GetRepository<ActivityAiSuggestion>().DeleteAsync(activitySuggestionId);
        }

        foreach (var missionSuggestionId in allMissionSuggestionIds)
        {
            await _unitOfWork.GetRepository<MissionAiSuggestion>().DeleteAsync(missionSuggestionId);
        }

        var clusterAnalysisIds = await _unitOfWork.Set<ClusterAiAnalysis>()
            .Where(analysis => analysis.ClusterId == clusterId)
            .Select(analysis => analysis.Id)
            .ToListAsync(cancellationToken);

        foreach (var clusterAnalysisId in clusterAnalysisIds)
        {
            await _unitOfWork.GetRepository<ClusterAiAnalysis>().DeleteAsync(clusterAnalysisId);
        }

        var rescueTeamSuggestionIds = await _unitOfWork.Set<RescueTeamAiSuggestion>()
            .Where(suggestion => suggestion.ClusterId == clusterId)
            .Select(suggestion => suggestion.Id)
            .ToListAsync(cancellationToken);

        foreach (var rescueTeamSuggestionId in rescueTeamSuggestionIds)
        {
            await _unitOfWork.GetRepository<RescueTeamAiSuggestion>().DeleteAsync(rescueTeamSuggestionId);
        }
    }
}
