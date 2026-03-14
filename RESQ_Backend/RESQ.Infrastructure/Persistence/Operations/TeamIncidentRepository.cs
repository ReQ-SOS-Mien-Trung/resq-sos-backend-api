using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Operations;

public class TeamIncidentRepository(ResQDbContext context) : ITeamIncidentRepository
{
    private readonly ResQDbContext _context = context;

    public async Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TeamIncidents
            .AsNoTracking()
            .FirstOrDefaultAsync(ti => ti.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.TeamIncidents
            .AsNoTracking()
            .Where(ti => ti.MissionTeam != null && ti.MissionTeam.MissionId == missionId)
            .OrderByDescending(ti => ti.ReportedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
    }

    public async Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.TeamIncidents
            .AsNoTracking()
            .Where(ti => ti.MissionTeamId == missionTeamId)
            .OrderByDescending(ti => ti.ReportedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
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
            Description = model.Description,
            Location = location,
            Status = model.Status.ToString(),
            ReportedBy = model.ReportedBy,
            ReportedAt = model.ReportedAt ?? DateTime.UtcNow
        };

        _context.TeamIncidents.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TeamIncidents
            .FirstOrDefaultAsync(ti => ti.Id == id, cancellationToken);

        if (entity is null) return;

        entity.Status = status.ToString();
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static TeamIncidentModel ToModel(TeamIncident entity) => new()
    {
        Id = entity.Id,
        MissionTeamId = entity.MissionTeamId ?? 0,
        Latitude = entity.Location?.Y,
        Longitude = entity.Location?.X,
        Description = entity.Description,
        Status = Enum.TryParse<TeamIncidentStatus>(entity.Status, out var s) ? s : TeamIncidentStatus.Reported,
        ReportedBy = entity.ReportedBy,
        ReportedAt = entity.ReportedAt
    };
}
