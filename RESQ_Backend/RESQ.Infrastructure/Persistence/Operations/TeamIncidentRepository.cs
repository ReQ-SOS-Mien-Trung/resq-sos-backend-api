using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class TeamIncidentRepository(IUnitOfWork unitOfWork) : ITeamIncidentRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.TeamIncidents
            .AsNoTracking()
            .OrderByDescending(ti => ti.ReportedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
    }

    public async Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<TeamIncident>()
            .GetByPropertyAsync(ti => ti.Id == id, tracked: false);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<TeamIncident>()
            .GetAllByPropertyAsync(ti => ti.MissionTeam != null && ti.MissionTeam.MissionId == missionId);

        return entities.OrderByDescending(ti => ti.ReportedAt).Select(ToModel);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<TeamIncident>()
            .GetAllByPropertyAsync(ti => ti.MissionTeamId == missionTeamId);

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
            MissionTeamId = model.MissionTeamId,
            Description   = model.Description,
            Location      = location,
            Status        = model.Status.ToString(),
            ReportedBy    = model.ReportedBy,
            ReportedAt    = model.ReportedAt ?? DateTime.UtcNow
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

    private static TeamIncidentModel ToModel(TeamIncident entity) => new()
    {
        Id            = entity.Id,
        MissionTeamId = entity.MissionTeamId ?? 0,
        Latitude      = entity.Location?.Y,
        Longitude     = entity.Location?.X,
        Description   = entity.Description,
        Status        = Enum.TryParse<TeamIncidentStatus>(entity.Status, out var s) ? s : TeamIncidentStatus.Reported,
        ReportedBy    = entity.ReportedBy,
        ReportedAt    = entity.ReportedAt
    };
}
