using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Mappers.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class TeamIncidentRepository(IUnitOfWork unitOfWork) : ITeamIncidentRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<TeamIncident>()
            .GetAllByPropertyAsync(includeProperties: "TeamIncidentActivities.MissionActivity");

        return entities.OrderByDescending(ti => ti.ReportedAt).Select(ToModel);
    }

    public async Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<TeamIncident>()
            .GetByPropertyAsync(ti => ti.Id == id, tracked: false, includeProperties: "TeamIncidentActivities.MissionActivity");

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<TeamIncident>()
            .GetAllByPropertyAsync(
                ti => ti.MissionTeam != null && ti.MissionTeam.MissionId == missionId,
                includeProperties: "TeamIncidentActivities.MissionActivity");

        return entities.OrderByDescending(ti => ti.ReportedAt).Select(ToModel);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<TeamIncident>()
            .GetAllByPropertyAsync(ti => ti.MissionTeamId == missionTeamId, includeProperties: "TeamIncidentActivities.MissionActivity");

        return entities.OrderByDescending(ti => ti.ReportedAt).Select(ToModel);
    }

    public async Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default)
    {
        NetTopologySuite.Geometries.Point? location = null;
        if (model.Latitude.HasValue && model.Longitude.HasValue)
        {
            location = new NetTopologySuite.Geometries.Point(
                model.Longitude.Value, model.Latitude.Value) { SRID = 4326 };
        }

        var entity = new TeamIncident
        {
            MissionTeamId    = model.MissionTeamId,
            MissionActivityId = model.MissionActivityId,
            Description      = model.Description,
            Location         = location,
            Status           = model.Status.ToString(),
            IncidentScope    = model.IncidentScope.ToString(),
            IncidentType     = model.IncidentType,
            DecisionCode     = model.DecisionCode,
            DetailJson       = model.DetailJson,
            PayloadVersion   = model.PayloadVersion,
            NeedSupportSos   = model.NeedSupportSos,
            NeedReassignActivity = model.NeedReassignActivity,
            SupportSosRequestId = model.SupportSosRequestId,
            ReportedBy       = model.ReportedBy,
            ReportedAt       = model.ReportedAt ?? DateTime.UtcNow,
            TeamIncidentActivities = model.AffectedActivities
                .OrderBy(activity => activity.OrderIndex)
                .Select(activity => new TeamIncidentActivity
                {
                    MissionActivityId = activity.MissionActivityId,
                    OrderIndex = activity.OrderIndex,
                    IsPrimary = activity.IsPrimary
                })
                .ToList()
        };

        await _unitOfWork.GetRepository<TeamIncident>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<TeamIncident>()
            .GetByPropertyAsync(ti => ti.Id == id, tracked: true);

        if (entity is null) return;

        entity.Status = status.ToString();
        await _unitOfWork.SaveAsync();
    }

    public async Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<TeamIncident>()
            .GetByPropertyAsync(ti => ti.Id == id, tracked: true);

        if (entity is null)
        {
            return;
        }

        entity.SupportSosRequestId = supportSosRequestId;
        await _unitOfWork.SaveAsync();
    }

    private static TeamIncidentModel ToModel(TeamIncident entity) => new()
    {
        Id               = entity.Id,
        MissionTeamId    = entity.MissionTeamId ?? 0,
        MissionActivityId = entity.MissionActivityId,
        IncidentScope    = Enum.TryParse<TeamIncidentScope>(entity.IncidentScope, out var scope) ? scope : TeamIncidentScope.Mission,
        IncidentType     = entity.IncidentType,
        DecisionCode     = entity.DecisionCode,
        Latitude         = entity.Location?.Y,
        Longitude        = entity.Location?.X,
        Description      = entity.Description,
        DetailJson       = entity.DetailJson,
        PayloadVersion   = entity.PayloadVersion ?? 1,
        NeedSupportSos   = entity.NeedSupportSos ?? false,
        NeedReassignActivity = entity.NeedReassignActivity ?? false,
        SupportSosRequestId = entity.SupportSosRequestId,
        Status           = string.Equals(entity.Status, "Acknowledged", StringComparison.OrdinalIgnoreCase)
            ? TeamIncidentStatus.InProgress
            : string.Equals(entity.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                ? TeamIncidentStatus.Resolved
            : Enum.TryParse<TeamIncidentStatus>(entity.Status, out var s) ? s : TeamIncidentStatus.Reported,
        ReportedBy       = entity.ReportedBy,
        ReportedAt       = entity.ReportedAt,
        AffectedActivities = entity.TeamIncidentActivities
            .OrderBy(activity => activity.OrderIndex)
            .Select(activity => new TeamIncidentAffectedActivityModel
            {
                MissionActivityId = activity.MissionActivityId,
                OrderIndex = activity.OrderIndex,
                IsPrimary = activity.IsPrimary,
                Step = activity.MissionActivity?.Step,
                ActivityType = activity.MissionActivity?.ActivityType,
                Status = activity.MissionActivity?.Status is null
                    ? null
                    : MissionActivityMapper.ToEnum(activity.MissionActivity.Status)
            })
            .ToList()
    };
}
