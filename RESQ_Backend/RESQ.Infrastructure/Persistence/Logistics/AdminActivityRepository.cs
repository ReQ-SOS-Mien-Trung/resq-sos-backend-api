using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class AdminActivityRepository(IUnitOfWork unitOfWork) : IAdminActivityRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PagedResult<AdminActivityListItem>> GetPagedAllAsync(
        string? activityType,
        int? depotId,
        List<string>? statuses,
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

        var activities = _unitOfWork.GetRepository<MissionActivity>().AsQueryable(false);
        var depots = _unitOfWork.GetRepository<Depot>().AsQueryable(false);

        // Base filter: must have mission and valid activity type
        activities = activities.Where(a =>
            a.MissionId.HasValue
            && a.Mission != null
            && a.ActivityType != null);

        // Activity type filter
        if (!string.IsNullOrWhiteSpace(activityType))
        {
            activities = activities.Where(a => EF.Functions.ILike(a.ActivityType!, activityType));
        }
        else
        {
            activities = activities.Where(a =>
                EF.Functions.ILike(a.ActivityType!, "COLLECT_SUPPLIES")
                || EF.Functions.ILike(a.ActivityType!, "RETURN_SUPPLIES"));
        }

        // Depot filter
        if (depotId.HasValue)
        {
            activities = activities.Where(a => a.DepotId == depotId.Value);
        }

        // Status filter
        if (statuses is { Count: > 0 })
        {
            var normalizedStatuses = statuses.Select(NormalizeStatus).ToList();
            activities = activities.Where(a =>
                a.Status != null && normalizedStatuses.Any(s => EF.Functions.ILike(a.Status, s)));
        }

        // Date range filter (on AssignedAt, fallback to CompletedAt)
        if (fromBoundary.HasValue)
        {
            activities = activities.Where(a =>
                (a.AssignedAt.HasValue && a.AssignedAt >= fromBoundary.Value)
                || (!a.AssignedAt.HasValue && a.CompletedAt.HasValue && a.CompletedAt >= fromBoundary.Value));
        }

        if (toBoundaryExclusive.HasValue)
        {
            activities = activities.Where(a =>
                (a.AssignedAt.HasValue && a.AssignedAt < toBoundaryExclusive.Value)
                || (!a.AssignedAt.HasValue && a.CompletedAt.HasValue && a.CompletedAt < toBoundaryExclusive.Value));
        }

        var query =
            from activity in activities
            join depot in depots on activity.DepotId equals (int?)depot.Id into depotJoin
            from depot in depotJoin.DefaultIfEmpty()
            orderby activity.CompletedAt.HasValue ? 0 : 1,
                activity.CompletedAt descending,
                activity.AssignedAt descending,
                activity.Step ?? int.MaxValue,
                activity.Id descending
            select new AdminActivityProjection
            {
                DepotId = activity.DepotId ?? 0,
                DepotName = activity.DepotName ?? (depot != null ? depot.Name : null),
                DepotAddress = activity.DepotAddress ?? (depot != null ? depot.Address : null),
                MissionId = activity.MissionId ?? 0,
                MissionType = activity.Mission!.MissionType,
                MissionStatus = activity.Mission.Status,
                MissionStartTime = activity.Mission.StartTime,
                MissionExpectedEndTime = activity.Mission.ExpectedEndTime,
                ActivityId = activity.Id,
                Step = activity.Step,
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

        var items = pagedItems.Select(x => new AdminActivityListItem
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

        return new PagedResult<AdminActivityListItem>(
            items,
            totalCount,
            normalizedPageNumber,
            normalizedPageSize);
    }

    private static string NormalizeStatus(string status) => status.ToLowerInvariant() switch
    {
        "ongoing" or "on_going" => "OnGoing",
        "pendingconfirmation" or "pending_confirmation" => "PendingConfirmation",
        _ => status
    };

    private static List<AdminActivityItemDetail> ParseItems(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return [];

        try
        {
            var items = JsonSerializer.Deserialize<List<SupplyItemJson>>(itemsJson, JsonOptions) ?? [];

            return items.Select(x => new AdminActivityItemDetail
            {
                ItemId = x.ItemId,
                ItemName = x.ItemName,
                Quantity = x.Quantity,
                Unit = x.Unit,
                ActualReturnedQuantity = x.ActualReturnedQuantity
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

    private sealed class SupplyItemJson
    {
        public int? ItemId { get; set; }
        public string? ItemName { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public int? ActualReturnedQuantity { get; set; }
    }

    private sealed class AdminActivityProjection
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
