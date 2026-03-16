using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionTeamRepository(IUnitOfWork unitOfWork) : IMissionTeamRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: false, includeProperties: "RescuerTeam");

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<MissionTeam>()
            .GetAllByPropertyAsync(mt => mt.MissionId == missionId, includeProperties: "RescuerTeam");

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
        var entity = await _unitOfWork.GetRepository<MissionTeam>()
            .GetByPropertyAsync(mt => mt.Id == id, tracked: true);

        if (entity is null) return;

        entity.Status = status;
        await _unitOfWork.SaveAsync();
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
                includeProperties: "RescuerTeam");

        return entities.OrderByDescending(mt => mt.AssignedAt).Select(ToModel);
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
