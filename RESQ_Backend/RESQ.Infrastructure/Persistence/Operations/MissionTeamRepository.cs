using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionTeamRepository(IUnitOfWork unitOfWork) : IMissionTeamRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: false, includeProperties: "RescuerTeam,RescuerTeam.AssemblyPoint,RescuerTeam.RescueTeamMembers.User,RescuerTeam.RescueTeamMembers.User.RescuerProfile,MissionTeamReport");

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<MissionTeam>()
            .GetAllByPropertyAsync(mt => mt.MissionId == missionId, includeProperties: "RescuerTeam,RescuerTeam.AssemblyPoint,RescuerTeam.RescueTeamMembers.User,RescuerTeam.RescueTeamMembers.User.RescuerProfile,MissionTeamReport");

        return entities.OrderBy(mt => mt.AssignedAt).Select(ToModel);
    }

    public async Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default)
    {
        var entity = new MissionTeam
        {
            MissionId     = model.MissionId,
            RescuerTeamId = model.RescuerTeamId,
            TeamType      = model.TeamType,
            Status        = model.Status,
            Note          = model.Note,
            AssignedAt    = model.AssignedAt ?? DateTime.UtcNow,
            CreatedAt     = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<MissionTeam>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(id, status, note: null, cancellationToken);
    }

    public async Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: true);

        if (entity is null) return;

        entity.Status = status;
        if (note is not null)
        {
            entity.Note = note;
        }
        await _unitOfWork.SaveAsync();
    }

    public async Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: true);

        if (entity is null) return;

        entity.CurrentLocation = new Point(longitude, latitude) { SRID = 4326 };
        entity.LocationUpdatedAt = DateTime.UtcNow;
        entity.LocationSource = locationSource;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: true);

        if (entity is null) return;

        entity.UnassignedAt = DateTime.UtcNow;
        entity.Status       = "Cancelled";
        await _unitOfWork.SaveAsync();
    }

    public async Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<MissionTeam>()
            .GetAllByPropertyAsync(
                mt => mt.RescuerTeamId == rescuerTeamId
                      && mt.MissionId != null
                      && mt.UnassignedAt == null
                      && mt.Status != "Cancelled",
                includeProperties: "RescuerTeam,RescuerTeam.AssemblyPoint,RescuerTeam.RescueTeamMembers.User,RescuerTeam.RescueTeamMembers.User.RescuerProfile,MissionTeamReport");

        return entities.OrderByDescending(mt => mt.AssignedAt).Select(ToModel);
    }

    public async Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(
                mt => mt.MissionId == missionId
                      && mt.RescuerTeamId == rescuerTeamId
                      && mt.UnassignedAt == null
                      && mt.Status != "Cancelled",
                tracked: false,
                    includeProperties: "RescuerTeam,RescuerTeam.AssemblyPoint,RescuerTeam.RescueTeamMembers.User,RescuerTeam.RescueTeamMembers.User.RescuerProfile,MissionTeamReport");

        return entity is null ? null : ToModel(entity);
    }

    private static MissionTeamModel ToModel(MissionTeam entity) => new()
    {
        Id = entity.Id,
        MissionId = entity.MissionId ?? 0,
        RescuerTeamId = entity.RescuerTeamId ?? 0,
        TeamType = entity.TeamType,
        Status = entity.Status,
        Note = entity.Note,
        AssignedAt = entity.AssignedAt,
        UnassignedAt = entity.UnassignedAt,
        Latitude = entity.CurrentLocation?.Y ?? entity.RescuerTeam?.AssemblyPoint?.Location?.Y,
        Longitude = entity.CurrentLocation?.X ?? entity.RescuerTeam?.AssemblyPoint?.Location?.X,
        LocationUpdatedAt = entity.LocationUpdatedAt,
        LocationSource = entity.CurrentLocation is not null
            ? (string.IsNullOrWhiteSpace(entity.LocationSource) ? "MissionTeam.CurrentLocation" : entity.LocationSource)
            : "AssemblyPoint",
        ReportStatus = entity.MissionTeamReport?.ReportStatus,
        ReportStartedAt = entity.MissionTeamReport?.StartedAt,
        ReportLastEditedAt = entity.MissionTeamReport?.LastEditedAt,
        ReportSubmittedAt = entity.MissionTeamReport?.SubmittedAt,
        TeamName = entity.RescuerTeam?.Name,
        TeamCode = entity.RescuerTeam?.Code,
        AssemblyPointName = entity.RescuerTeam?.AssemblyPoint?.Name,
        TeamStatus = entity.RescuerTeam?.Status,
        MaxMembers = entity.RescuerTeam?.MaxMembers,
        MemberCount = entity.RescuerTeam?.RescueTeamMembers.Count ?? 0,
        AssemblyDate = entity.RescuerTeam?.AssemblyDate,
        RescueTeamMembers = entity.RescuerTeam?.RescueTeamMembers.Select(m => new MissionTeamMemberInfo
        {
            UserId = m.UserId,
            FullName = m.User is null ? null : $"{m.User.LastName} {m.User.FirstName}".Trim(),
            Username = m.User?.Username,
            Phone = m.User?.Phone,
            AvatarUrl = m.User?.AvatarUrl,
            RescuerType = m.User?.RescuerProfile?.RescuerType,
            RoleInTeam = m.RoleInTeam,
            IsLeader = m.IsLeader,
            Status = m.Status,
            CheckedIn = m.CheckedIn
        }).ToList() ?? []
    };
}
