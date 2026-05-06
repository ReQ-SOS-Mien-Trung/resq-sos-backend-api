using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Mappers.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionActivityRepository(IUnitOfWork unitOfWork) : IMissionActivityRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private static readonly HashSet<string> AssemblyPointDependentActivityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EVACUATE",
        "RETURN_ASSEMBLY_POINT"
    };

    public async Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionActivity>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "AssemblyPoint");

        return entity is null ? null : MissionActivityMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<MissionActivity>()
            .GetAllByPropertyAsync(x => x.MissionId == missionId, includeProperties: "AssemblyPoint");

        return entities
            .OrderBy(x => x.Step)
            .Select(MissionActivityMapper.ToDomain);
    }

    public async Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var entities = await _unitOfWork.GetRepository<MissionActivity>()
            .GetAllByPropertyAsync(x => x.SosRequestId.HasValue && ids.Contains(x.SosRequestId.Value), includeProperties: "AssemblyPoint");

        return entities
            .OrderBy(x => x.Step)
            .Select(MissionActivityMapper.ToDomain);
    }

    public async Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
    {
        var openStatuses = new[]
        {
            MissionActivityStatus.Planned.ToString(),
            MissionActivityStatus.OnGoing.ToString(),
            MissionActivityStatus.PendingConfirmation.ToString()
        };

        var entities = await _unitOfWork.Set<MissionActivity>()
            .AsNoTracking()
            .Where(x => x.AssemblyPointId == assemblyPointId && openStatuses.Contains(x.Status))
            .Include(x => x.AssemblyPoint)
            .OrderBy(x => x.Step)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(MissionActivityMapper.ToDomain).ToList();
    }

    public async Task<List<AssemblyPointUnavailableRescueTeamImpactDto>> GetReassignableAssemblyPointImpactsAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var openStatuses = OpenStatusStrings();

        var entities = await _unitOfWork.Set<MissionActivity>()
            .AsNoTracking()
            .Where(x => x.AssemblyPointId == assemblyPointId && openStatuses.Contains(x.Status ?? string.Empty))
            .Include(x => x.MissionTeam)
                .ThenInclude(t => t!.RescuerTeam)
                    .ThenInclude(t => t!.RescueTeamMembers)
            .OrderBy(x => x.MissionTeamId)
            .ThenBy(x => x.Step)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var reassignable = entities
            .Where(x => IsAssemblyPointPhysicalActivity(x.ActivityType))
            .Where(x => x.MissionTeam?.RescuerTeamId is not null)
            .ToList();

        return reassignable
            .GroupBy(x => x.MissionTeam!.RescuerTeamId!.Value)
            .Select(g =>
            {
                var first = g.First();
                var rescueTeam = first.MissionTeam?.RescuerTeam;
                var acceptedStatus = TeamMemberStatus.Accepted.ToString();
                return new AssemblyPointUnavailableRescueTeamImpactDto
                {
                    RescueTeamId = rescueTeam?.Id,
                    RescueTeamCode = rescueTeam?.Code,
                    RescueTeamName = rescueTeam?.Name,
                    RescueTeamStatus = rescueTeam?.Status,
                    MissionTeamId = first.MissionTeamId,
                    ImpactReasons =
                    [
                        AssemblyPointUnavailableImpactReason.HasMissionActivityTargetingUnavailablePoint
                    ],
                    MemberUserIds = rescueTeam?.RescueTeamMembers
                        .Where(m => m.Status == acceptedStatus)
                        .Select(m => m.UserId)
                        .Distinct()
                        .ToList() ?? [],
                    Activities = g.Select(x => new AssemblyPointUnavailableMissionActivityDto
                    {
                        MissionActivityId = x.Id,
                        MissionId = x.MissionId,
                        MissionTeamId = x.MissionTeamId,
                        RescueTeamId = rescueTeam?.Id,
                        RescueTeamCode = rescueTeam?.Code,
                        RescueTeamName = rescueTeam?.Name,
                        Step = x.Step,
                        ActivityType = x.ActivityType,
                        Status = x.Status ?? string.Empty,
                        Description = x.Description
                    }).ToList()
                };
            })
            .ToList();
    }

    public async Task<HashSet<int>> ReassignAssemblyPointAsync(
        int sourceAssemblyPointId,
        IReadOnlyDictionary<int, int> activityAssignments,
        Guid changedBy,
        CancellationToken cancellationToken = default)
    {
        if (activityAssignments.Count == 0)
        {
            return [];
        }

        var ids = activityAssignments.Keys.ToList();
        var openStatuses = OpenStatusStrings();
        var entities = await _unitOfWork.SetTracked<MissionActivity>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count != ids.Count)
        {
            var foundIds = entities.Select(x => x.Id).ToHashSet();
            var missing = ids.Where(id => !foundIds.Contains(id));
            throw new InvalidOperationException($"Không tìm thấy mission activities: {string.Join(", ", missing)}.");
        }

        foreach (var entity in entities)
        {
            if (entity.AssemblyPointId != sourceAssemblyPointId)
            {
                throw new InvalidOperationException($"Mission activity #{entity.Id} không còn thuộc điểm tập kết nguồn.");
            }

            if (!openStatuses.Contains(entity.Status ?? string.Empty))
            {
                throw new InvalidOperationException($"Mission activity #{entity.Id} không còn ở trạng thái có thể điều phối.");
            }

            if (!IsAssemblyPointPhysicalActivity(entity.ActivityType))
            {
                throw new InvalidOperationException($"Mission activity #{entity.Id} không phải activity phụ thuộc vật lý vào điểm tập kết.");
            }

            entity.AssemblyPointId = activityAssignments[entity.Id];
            entity.LastDecisionBy = changedBy;
        }

        return entities.Select(x => x.Id).ToHashSet();
    }

    public async Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
    {
        var entity = MissionActivityMapper.ToEntity(activity);
        entity.AssignedAt = DateTime.UtcNow;
        await _unitOfWork.GetRepository<MissionActivity>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionActivity>()
            .GetByPropertyAsync(x => x.Id == activity.Id, tracked: true);

        if (entity is null) return;

        entity.Step = activity.Step;
        entity.ActivityType = activity.ActivityType;
        entity.Description = activity.Description;
        entity.Target = MissionActivityMapper.EnsureValidJson(activity.Target);
        entity.Items = MissionActivityMapper.EnsureValidJson(activity.Items);
        entity.AssemblyPointId = activity.AssemblyPointId;
        if (activity.LastDecisionBy.HasValue)
        {
            entity.LastDecisionBy = activity.LastDecisionBy;
        }

        if (activity.TargetLatitude.HasValue && activity.TargetLongitude.HasValue)
        {
            entity.TargetLocation = new NetTopologySuite.Geometries.Point(
                activity.TargetLongitude.Value, activity.TargetLatitude.Value) { SRID = 4326 };
        }

        await _unitOfWork.GetRepository<MissionActivity>().UpdateAsync(entity);
    }

    public async Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionActivity>()
            .GetByPropertyAsync(x => x.Id == activityId, tracked: true);

        if (entity is null) return;

        entity.Status = MissionActivityMapper.ToDbString(status);
        entity.LastDecisionBy = decisionBy;

        if (status == MissionActivityStatus.Succeed || status == MissionActivityStatus.Failed)
            entity.CompletedAt = DateTime.UtcNow;

        if (status == MissionActivityStatus.Succeed)
            entity.CompletedBy = decisionBy;

        if (!string.IsNullOrWhiteSpace(imageUrl))
            entity.ImageUrl = imageUrl.Trim();

        await _unitOfWork.GetRepository<MissionActivity>().UpdateAsync(entity);
    }

    public async Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionActivity>()
            .GetByPropertyAsync(x => x.Id == activityId, tracked: true);

        if (entity is null) return;

        entity.MissionTeamId = missionTeamId;
        entity.AssignedAt = DateTime.UtcNow;
        await _unitOfWork.GetRepository<MissionActivity>().UpdateAsync(entity);
    }

    public async Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default)
    {
        var ids = activityIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var entities = await _unitOfWork.SetTracked<MissionActivity>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            entity.MissionTeamId = null;
            entity.AssignedAt = null;
            entity.Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Planned);
            entity.LastDecisionBy = decisionBy;
            entity.CompletedAt = null;
            entity.CompletedBy = null;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<MissionActivity>().DeleteAsyncById(id);
        await _unitOfWork.SaveAsync();
    }

    internal static string[] OpenStatusStrings() =>
    [
        MissionActivityStatus.Planned.ToString(),
        MissionActivityStatus.OnGoing.ToString(),
        MissionActivityStatus.PendingConfirmation.ToString()
    ];

    internal static bool IsAssemblyPointPhysicalActivity(string? activityType)
    {
        if (string.IsNullOrWhiteSpace(activityType))
        {
            return false;
        }

        return AssemblyPointDependentActivityTypes.Contains(activityType.Trim());
    }
}
