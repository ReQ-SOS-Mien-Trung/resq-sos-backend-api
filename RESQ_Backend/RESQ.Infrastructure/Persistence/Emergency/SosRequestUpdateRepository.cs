using System.Text.Json;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestUpdateRepository(IUnitOfWork unitOfWork) : ISosRequestUpdateRepository
{
    private const string IncidentUpdateType = "Incident";
    private const string VictimUpdateType = "VictimUpdate";
    private const string VictimUpdatedStatus = "VictimUpdated";

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default)
    {
        var entity = new SosRequestUpdate
        {
            SosRequestId = update.SosRequestId,
            Type = VictimUpdateType,
            Status = VictimUpdatedStatus,
            Content = JsonSerializer.Serialize(new VictimUpdateContent
            {
                PacketId = update.PacketId,
                Latitude = update.Location?.Latitude,
                Longitude = update.Location?.Longitude,
                LocationAccuracy = update.LocationAccuracy,
                SosType = update.SosType,
                RawMessage = update.RawMessage,
                StructuredData = update.StructuredData,
                NetworkMetadata = update.NetworkMetadata,
                SenderInfo = update.SenderInfo,
                VictimInfo = update.VictimInfo,
                ReporterInfo = update.ReporterInfo,
                IsSentOnBehalf = update.IsSentOnBehalf,
                OriginId = update.OriginId,
                Timestamp = update.Timestamp,
                ClientCreatedAt = update.ClientCreatedAt,
                UpdatedByUserId = update.UpdatedByUserId,
                UpdatedByMode = update.UpdatedByMode
            }),
            CreatedAt = update.UpdatedAt == default ? DateTime.UtcNow : update.UpdatedAt
        };

        await _unitOfWork.GetRepository<SosRequestUpdate>().AddAsync(entity);
    }

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

    public async Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(
        IEnumerable<int> teamIncidentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = teamIncidentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<int>>();
        }

        var entities = await _unitOfWork.GetRepository<SosRequestUpdate>()
            .GetAllByPropertyAsync(update => update.SosRequestId.HasValue && update.Type == IncidentUpdateType);

        return entities
            .Select(entity => new
            {
                Incident = ParseContent(entity.Content)?.TeamIncidentId,
                SosRequestId = entity.SosRequestId
            })
            .Where(item => item.Incident.HasValue
                && item.SosRequestId.HasValue
                && ids.Contains(item.Incident.Value))
            .GroupBy(item => item.Incident!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<int>)group
                    .Select(item => item.SosRequestId!.Value)
                    .Distinct()
                    .ToList());
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<int>>();
        }

        var entities = await _unitOfWork.GetRepository<SosRequestUpdate>()
            .GetAllByPropertyAsync(update => update.SosRequestId.HasValue
                && ids.Contains(update.SosRequestId.Value)
                && update.Type == IncidentUpdateType);

        return entities
            .Select(entity => new
            {
                SosRequestId = entity.SosRequestId,
                Incident = ParseContent(entity.Content)?.TeamIncidentId
            })
            .Where(item => item.SosRequestId.HasValue && item.Incident.HasValue)
            .GroupBy(item => item.SosRequestId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<int>)group
                    .Select(item => item.Incident!.Value)
                    .Distinct()
                    .ToList());
    }

    public async Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, SosRequestVictimUpdateModel>();
        }

        var entities = await _unitOfWork.GetRepository<SosRequestUpdate>()
            .GetAllByPropertyAsync(update => update.SosRequestId.HasValue
                && ids.Contains(update.SosRequestId.Value)
                && update.Type == VictimUpdateType);

        return entities
            .GroupBy(update => update.SosRequestId!.Value)
            .ToDictionary(
                group => group.Key,
                group => ToVictimUpdateModel(group
                    .OrderByDescending(update => update.CreatedAt)
                    .ThenByDescending(update => update.Id)
                    .First()));
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

    private static SosRequestVictimUpdateModel ToVictimUpdateModel(SosRequestUpdate entity)
    {
        var content = ParseVictimUpdateContent(entity.Content);

        GeoLocation? location = null;
        if (content?.Latitude.HasValue == true && content.Longitude.HasValue)
        {
            location = new GeoLocation(content.Latitude.Value, content.Longitude.Value);
        }

        return new SosRequestVictimUpdateModel
        {
            Id = entity.Id,
            SosRequestId = entity.SosRequestId ?? 0,
            PacketId = content?.PacketId,
            Location = location,
            LocationAccuracy = content?.LocationAccuracy,
            SosType = content?.SosType,
            RawMessage = content?.RawMessage ?? string.Empty,
            StructuredData = content?.StructuredData,
            NetworkMetadata = content?.NetworkMetadata,
            SenderInfo = content?.SenderInfo,
            VictimInfo = content?.VictimInfo,
            ReporterInfo = content?.ReporterInfo,
            IsSentOnBehalf = content?.IsSentOnBehalf ?? false,
            OriginId = content?.OriginId,
            Timestamp = content?.Timestamp,
            ClientCreatedAt = content?.ClientCreatedAt,
            UpdatedByUserId = content?.UpdatedByUserId ?? Guid.Empty,
            UpdatedAt = entity.CreatedAt ?? DateTime.UtcNow,
            UpdatedByMode = content?.UpdatedByMode
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

    private static VictimUpdateContent? ParseVictimUpdateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<VictimUpdateContent>(content);
        }
        catch
        {
            return null;
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

    private sealed class VictimUpdateContent
    {
        public Guid? PacketId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? LocationAccuracy { get; set; }
        public string? SosType { get; set; }
        public string? RawMessage { get; set; }
        public string? StructuredData { get; set; }
        public string? NetworkMetadata { get; set; }
        public string? SenderInfo { get; set; }
        public string? VictimInfo { get; set; }
        public string? ReporterInfo { get; set; }
        public bool IsSentOnBehalf { get; set; }
        public string? OriginId { get; set; }
        public long? Timestamp { get; set; }
        public DateTime? ClientCreatedAt { get; set; }
        public Guid? UpdatedByUserId { get; set; }
        public string? UpdatedByMode { get; set; }
    }
}
