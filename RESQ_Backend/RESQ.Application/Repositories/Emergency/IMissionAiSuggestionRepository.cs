using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface IMissionAiSuggestionRepository
{
    /// <summary>
    /// Lưu mission suggestion (kèm activities) vào DB. Trả về ID vừa tạo.
    /// </summary>
    Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default);
    Task SavePipelineSnapshotAsync(
        int suggestionId,
        MissionSuggestionMetadata metadata,
        CancellationToken cancellationToken = default);
    Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdsAsync(IEnumerable<int> clusterIds, CancellationToken cancellationToken = default);
}
