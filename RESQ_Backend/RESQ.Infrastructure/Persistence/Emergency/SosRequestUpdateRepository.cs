using System.Text.Json;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestUpdateRepository(IUnitOfWork unitOfWork) : ISosRequestUpdateRepository
{
    private const string IncidentUpdateType = "Incident";

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequestUpdate>();

        foreach (var update in updates)
        {
            var entity = new SosRequestUpdate
            {
                SosRequestId = update.SosRequestId,
                Type = IncidentUpdateType,
                Status = "Incident",
                Content = JsonSerializer.Serialize(new IncidentUpdateContent
                {
                    TeamIncidentId = update.TeamIncidentId,
                    MissionId = update.MissionId,
                    MissionTeamId = update.MissionTeamId,
                    MissionActivityId = update.MissionActivityId,
                    IncidentScope = update.IncidentScope,
                    Note = update.Note,
                    ReportedById = update.ReportedById,
                    TeamName = update.TeamName,
                    ActivityType = update.ActivityType
                }),
                CreatedAt = update.CreatedAt == default ? DateTime.UtcNow : update.CreatedAt
            };

            await repository.AddAsync(entity);
        }
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>();
        }

        var entities = await _unitOfWork.GetRepository<SosRequestUpdate>()
            .GetAllByPropertyAsync(update => update.SosRequestId.HasValue
                && ids.Contains(update.SosRequestId.Value)
                && update.Type == IncidentUpdateType);

        return entities
            .Select(ToModel)
            .GroupBy(update => update.SosRequestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SosRequestIncidentUpdateModel>)group
                    .OrderByDescending(update => update.CreatedAt)
                    .ThenByDescending(update => update.Id)
                    .ToList());
    }

    private static SosRequestIncidentUpdateModel ToModel(SosRequestUpdate entity)
    {
        var content = ParseContent(entity.Content);

        return new SosRequestIncidentUpdateModel
        {
            Id = entity.Id,
            SosRequestId = entity.SosRequestId ?? 0,
            TeamIncidentId = content?.TeamIncidentId,
            MissionId = content?.MissionId,
            MissionTeamId = content?.MissionTeamId,
            MissionActivityId = content?.MissionActivityId,
            IncidentScope = content?.IncidentScope,
            Note = content?.Note ?? entity.Content ?? string.Empty,
            ReportedById = content?.ReportedById,
            CreatedAt = entity.CreatedAt ?? DateTime.UtcNow,
            TeamName = content?.TeamName,
            ActivityType = content?.ActivityType
        };
    }

    private static IncidentUpdateContent? ParseContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IncidentUpdateContent>(content);
        }
        catch
        {
            return new IncidentUpdateContent { Note = content };
        }
    }

    private sealed class IncidentUpdateContent
    {
        public int? TeamIncidentId { get; set; }
        public int? MissionId { get; set; }
        public int? MissionTeamId { get; set; }
        public int? MissionActivityId { get; set; }
        public string? IncidentScope { get; set; }
        public string? Note { get; set; }
        public Guid? ReportedById { get; set; }
        public string? TeamName { get; set; }
        public string? ActivityType { get; set; }
    }
}