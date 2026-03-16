using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionTeamRepository(IUnitOfWork unitOfWork, ResQDbContext context) : IMissionTeamRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResQDbContext _context = context;

    public async Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MissionTeams
            .AsNoTracking()
            .Include(mt => mt.RescuerTeam)
            .FirstOrDefaultAsync(mt => mt.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.MissionTeams
            .AsNoTracking()
            .Include(mt => mt.RescuerTeam)
            .Where(mt => mt.MissionId == missionId)
            .OrderBy(mt => mt.AssignedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
    }

    public async Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default)
    {
        var entity = new MissionTeam
        {
            MissionId = model.MissionId,
            RescuerTeamId = model.RescuerTeamId,
            TeamType = model.TeamType,
            Status = model.Status,
            Note = model.Note,
            AssignedAt = model.AssignedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.MissionTeams.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MissionTeams
            .FirstOrDefaultAsync(mt => mt.Id == id, cancellationToken);

        if (entity is null) return;

        entity.Status = status;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MissionTeams
            .FirstOrDefaultAsync(mt => mt.Id == id, cancellationToken);

        if (entity is null) return;

        entity.UnassignedAt = DateTime.UtcNow;
        entity.Status = "Cancelled";
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.MissionTeams
            .AsNoTracking()
            .Include(mt => mt.RescuerTeam)
            .Where(mt => mt.RescuerTeamId == rescuerTeamId
                         && mt.MissionId != null
                         && mt.UnassignedAt == null
                         && mt.Status != "Cancelled")
            .OrderByDescending(mt => mt.AssignedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
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
        TeamName = entity.RescuerTeam?.Name,
        TeamCode = entity.RescuerTeam?.Code
    };
}
