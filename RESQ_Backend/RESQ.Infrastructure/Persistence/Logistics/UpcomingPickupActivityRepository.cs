using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class UpcomingPickupActivityRepository(IUnitOfWork unitOfWork) : IUpcomingPickupActivityRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PagedResult<UpcomingPickupActivityListItem>> GetPagedByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 20 : pageSize;

        var activities = _unitOfWork.GetRepository<MissionActivity>().AsQueryable(false);
        var depots = _unitOfWork.GetRepository<Depot>().AsQueryable(false);

        var query =
            from activity in activities
            join depot in depots on activity.DepotId equals (int?)depot.Id into depotJoin
            from depot in depotJoin.DefaultIfEmpty()
            where activity.DepotId == depotId
                  && activity.MissionId.HasValue
                  && activity.Mission != null
                  && activity.ActivityType != null
                  && EF.Functions.ILike(activity.ActivityType, "COLLECT_SUPPLIES")
                  && activity.Status != null
                  && (EF.Functions.ILike(activity.Status, "Planned")
                      || EF.Functions.ILike(activity.Status, "OnGoing")
                      || EF.Functions.ILike(activity.Status, "on_going"))
                  && activity.Mission.IsCompleted != true
                  && (activity.Mission.Status == null
                      || (!EF.Functions.ILike(activity.Mission.Status, "Completed")
                          && !EF.Functions.ILike(activity.Mission.Status, "Incompleted")))
            orderby (EF.Functions.ILike(activity.Status!, "OnGoing")
                     || EF.Functions.ILike(activity.Status!, "on_going")) ? 0 : 1,
                activity.Mission!.StartTime ?? DateTime.MaxValue,
                activity.Step ?? int.MaxValue,
                activity.Id
            select new UpcomingPickupActivityProjection
            {
                DepotId = activity.DepotId ?? depotId,
                DepotName = activity.DepotName ?? (depot != null ? depot.Name : null),
                MissionId = activity.MissionId ?? 0,
                MissionType = activity.Mission!.MissionType,
                MissionStatus = activity.Mission.Status,
                MissionStartTime = activity.Mission.StartTime,
                MissionExpectedEndTime = activity.Mission.ExpectedEndTime,
                ActivityId = activity.Id,
                Step = activity.Step,
                ActivityCode = activity.ActivityCode,
                ActivityType = activity.ActivityType,
                Description = activity.Description,
                Priority = activity.Priority,
                EstimatedTime = activity.EstimatedTime,
                Status = activity.Status,
                AssignedAt = activity.AssignedAt,
                MissionTeamId = activity.MissionTeamId,
                RescueTeamId = activity.MissionTeam != null ? activity.MissionTeam.RescuerTeamId : null,
                RescueTeamName = activity.MissionTeam != null && activity.MissionTeam.RescuerTeam != null
                    ? activity.MissionTeam.RescuerTeam.Name
                    : null,
                TeamType = activity.MissionTeam != null
                    ? activity.MissionTeam.TeamType
                        ?? (activity.MissionTeam.RescuerTeam != null
                            ? activity.MissionTeam.RescuerTeam.TeamType
                            : null)
                    : null,
                ItemsJson = activity.Items
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var pagedItems = await query
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = pagedItems.Select(x => new UpcomingPickupActivityListItem
        {
            DepotId = x.DepotId,
            DepotName = x.DepotName,
            MissionId = x.MissionId,
            MissionType = x.MissionType,
            MissionStatus = x.MissionStatus,
            MissionStartTime = x.MissionStartTime,
            MissionExpectedEndTime = x.MissionExpectedEndTime,
            ActivityId = x.ActivityId,
            Step = x.Step,
            ActivityCode = x.ActivityCode,
            ActivityType = x.ActivityType,
            Description = x.Description,
            Priority = x.Priority,
            EstimatedTime = x.EstimatedTime,
            Status = x.Status,
            AssignedAt = x.AssignedAt,
            MissionTeamId = x.MissionTeamId,
            RescueTeamId = x.RescueTeamId,
            RescueTeamName = x.RescueTeamName,
            TeamType = x.TeamType,
            Items = ParseItems(x.ItemsJson)
        }).ToList();

        return new PagedResult<UpcomingPickupActivityListItem>(
            items,
            totalCount,
            normalizedPageNumber,
            normalizedPageSize);
    }

    public async Task<PagedResult<PickupHistoryActivityListItem>> GetHistoryPagedByDepotIdAsync(
        int depotId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 20 : pageSize;
        var fromBoundary = fromDate.HasValue
            ? DateTime.SpecifyKind(fromDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : (DateTime?)null;
        var toBoundaryExclusive = toDate.HasValue
            ? DateTime.SpecifyKind(toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : (DateTime?)null;

        var activities = _unitOfWork.GetRepository<MissionActivity>()
            .AsQueryable(false)
            .Where(activity =>
                activity.DepotId == depotId
                && activity.MissionId.HasValue
                && activity.Mission != null
                && activity.ActivityType != null
                && EF.Functions.ILike(activity.ActivityType, "COLLECT_SUPPLIES")
                && activity.Status != null
                && EF.Functions.ILike(activity.Status, "Succeed")
                && activity.CompletedAt.HasValue);

        if (fromBoundary.HasValue)
        {
            activities = activities.Where(activity => activity.CompletedAt >= fromBoundary.Value);
        }

        if (toBoundaryExclusive.HasValue)
        {
            activities = activities.Where(activity => activity.CompletedAt < toBoundaryExclusive.Value);
        }

        var depots = _unitOfWork.GetRepository<Depot>().AsQueryable(false);

        var query =
            from activity in activities
            join depot in depots on activity.DepotId equals (int?)depot.Id into depotJoin
            from depot in depotJoin.DefaultIfEmpty()
            orderby activity.CompletedAt descending, activity.Step, activity.Id descending
            select new PickupHistoryActivityProjection
            {
                DepotId = activity.DepotId ?? depotId,
                DepotName = activity.DepotName ?? (depot != null ? depot.Name : null),
                DepotAddress = activity.DepotAddress ?? (depot != null ? depot.Address : null),
                MissionId = activity.MissionId ?? 0,
                MissionType = activity.Mission!.MissionType,
                MissionStatus = activity.Mission.Status,
                MissionStartTime = activity.Mission.StartTime,
                MissionExpectedEndTime = activity.Mission.ExpectedEndTime,
                ActivityId = activity.Id,
                Step = activity.Step,
                ActivityCode = activity.ActivityCode,
                ActivityType = activity.ActivityType,
                Description = activity.Description,
                Priority = activity.Priority,
                EstimatedTime = activity.EstimatedTime,
                Status = activity.Status,
                AssignedAt = activity.AssignedAt,
                CompletedAt = activity.CompletedAt,
                CompletedBy = activity.CompletedBy,
                CompletedByFirstName = activity.CompletedByUser != null ? activity.CompletedByUser.FirstName : null,
                CompletedByLastName = activity.CompletedByUser != null ? activity.CompletedByUser.LastName : null,
                MissionTeamId = activity.MissionTeamId,
                RescueTeamId = activity.MissionTeam != null ? activity.MissionTeam.RescuerTeamId : null,
                RescueTeamName = activity.MissionTeam != null && activity.MissionTeam.RescuerTeam != null
                    ? activity.MissionTeam.RescuerTeam.Name
                    : null,
                TeamType = activity.MissionTeam != null
                    ? activity.MissionTeam.TeamType
                        ?? (activity.MissionTeam.RescuerTeam != null
                            ? activity.MissionTeam.RescuerTeam.TeamType
                            : null)
                    : null,
                ItemsJson = activity.Items
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var pagedItems = await query
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = pagedItems.Select(x => new PickupHistoryActivityListItem
        {
            DepotId = x.DepotId,
            DepotName = x.DepotName,
            DepotAddress = x.DepotAddress,
            MissionId = x.MissionId,
            MissionType = x.MissionType,
            MissionStatus = x.MissionStatus,
            MissionStartTime = x.MissionStartTime,
            MissionExpectedEndTime = x.MissionExpectedEndTime,
            ActivityId = x.ActivityId,
            Step = x.Step,
            ActivityCode = x.ActivityCode,
            ActivityType = x.ActivityType,
            Description = x.Description,
            Priority = x.Priority,
            EstimatedTime = x.EstimatedTime,
            Status = x.Status,
            AssignedAt = x.AssignedAt,
            CompletedAt = x.CompletedAt,
            CompletedBy = x.CompletedBy,
            CompletedByName = FormatFullName(x.CompletedByLastName, x.CompletedByFirstName),
            MissionTeamId = x.MissionTeamId,
            RescueTeamId = x.RescueTeamId,
            RescueTeamName = x.RescueTeamName,
            TeamType = x.TeamType,
            Items = ParseItems(x.ItemsJson)
        }).ToList();

        return new PagedResult<PickupHistoryActivityListItem>(
            items,
            totalCount,
            normalizedPageNumber,
            normalizedPageSize);
    }

    private static List<UpcomingPickupItemDetail> ParseItems(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return [];

        try
        {
            var items = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOptions) ?? [];

            return items.Select(x => new UpcomingPickupItemDetail
            {
                ItemId = x.ItemId,
                ItemName = x.ItemName,
                Quantity = x.Quantity,
                Unit = x.Unit
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? FormatFullName(string? lastName, string? firstName)
    {
        var fullName = string.Join(" ", new[] { lastName, firstName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }

    private sealed class UpcomingPickupActivityProjection
    {
        public int DepotId { get; set; }
        public string? DepotName { get; set; }
        public int MissionId { get; set; }
        public string? MissionType { get; set; }
        public string? MissionStatus { get; set; }
        public DateTime? MissionStartTime { get; set; }
        public DateTime? MissionExpectedEndTime { get; set; }
        public int ActivityId { get; set; }
        public int? Step { get; set; }
        public string? ActivityCode { get; set; }
        public string? ActivityType { get; set; }
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public int? EstimatedTime { get; set; }
        public string? Status { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int? MissionTeamId { get; set; }
        public int? RescueTeamId { get; set; }
        public string? RescueTeamName { get; set; }
        public string? TeamType { get; set; }
        public string? ItemsJson { get; set; }
    }

    private sealed class PickupHistoryActivityProjection
    {
        public int DepotId { get; set; }
        public string? DepotName { get; set; }
        public string? DepotAddress { get; set; }
        public int MissionId { get; set; }
        public string? MissionType { get; set; }
        public string? MissionStatus { get; set; }
        public DateTime? MissionStartTime { get; set; }
        public DateTime? MissionExpectedEndTime { get; set; }
        public int ActivityId { get; set; }
        public int? Step { get; set; }
        public string? ActivityCode { get; set; }
        public string? ActivityType { get; set; }
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public int? EstimatedTime { get; set; }
        public string? Status { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public string? CompletedByFirstName { get; set; }
        public string? CompletedByLastName { get; set; }
        public int? MissionTeamId { get; set; }
        public int? RescueTeamId { get; set; }
        public string? RescueTeamName { get; set; }
        public string? TeamType { get; set; }
        public string? ItemsJson { get; set; }
    }
}
