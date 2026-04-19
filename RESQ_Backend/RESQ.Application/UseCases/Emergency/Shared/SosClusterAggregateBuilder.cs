using System.Text.Json;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Shared;

internal sealed class SosClusterAggregateSnapshot
{
    public double? CenterLatitude { get; init; }
    public double? CenterLongitude { get; init; }
    public string? SeverityLevel { get; init; }
    public int? VictimEstimated { get; init; }
    public int? ChildrenCount { get; init; }
    public int? ElderlyCount { get; init; }
    public List<int> SosRequestIds { get; init; } = [];
}

internal static class SosClusterAggregateBuilder
{
    public static SosClusterAggregateSnapshot Build(IEnumerable<SosRequestModel> sosRequests)
    {
        var resolvedRequests = sosRequests.ToList();

        var validCoords = resolvedRequests
            .Where(request => request.Location?.Latitude != null && request.Location?.Longitude != null)
            .ToList();

        double? centerLatitude = validCoords.Count > 0
            ? validCoords.Average(request => request.Location!.Latitude)
            : null;
        double? centerLongitude = validCoords.Count > 0
            ? validCoords.Average(request => request.Location!.Longitude)
            : null;

        var priorities = resolvedRequests
            .Where(request => request.PriorityLevel.HasValue)
            .Select(request => request.PriorityLevel!.Value)
            .ToList();

        var severityLevel = priorities.Count > 0
            ? priorities.Max().ToString()
            : null;

        var peopleCount = ParsePeopleCount(resolvedRequests);

        return new SosClusterAggregateSnapshot
        {
            CenterLatitude = centerLatitude,
            CenterLongitude = centerLongitude,
            SeverityLevel = severityLevel,
            VictimEstimated = peopleCount.HasPeopleCount
                ? peopleCount.TotalAdult + peopleCount.TotalChild + peopleCount.TotalElderly
                : null,
            ChildrenCount = peopleCount.HasPeopleCount ? peopleCount.TotalChild : null,
            ElderlyCount = peopleCount.HasPeopleCount ? peopleCount.TotalElderly : null,
            SosRequestIds = resolvedRequests.Select(request => request.Id).Distinct().ToList()
        };
    }

    public static void ApplyToCluster(SosClusterModel cluster, SosClusterAggregateSnapshot snapshot)
    {
        cluster.CenterLatitude = snapshot.CenterLatitude;
        cluster.CenterLongitude = snapshot.CenterLongitude;
        cluster.SeverityLevel = snapshot.SeverityLevel;
        cluster.VictimEstimated = snapshot.VictimEstimated;
        cluster.ChildrenCount = snapshot.ChildrenCount;
        cluster.ElderlyCount = snapshot.ElderlyCount;
        cluster.SosRequestIds = snapshot.SosRequestIds;
    }

    private static (bool HasPeopleCount, int TotalAdult, int TotalChild, int TotalElderly) ParsePeopleCount(
        IEnumerable<SosRequestModel> sosRequests)
    {
        var totalAdult = 0;
        var totalChild = 0;
        var totalElderly = 0;
        var hasPeopleCount = false;

        foreach (var sosRequest in sosRequests)
        {
            if (string.IsNullOrWhiteSpace(sosRequest.StructuredData))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(sosRequest.StructuredData);
                JsonElement peopleCount;
                var hasPeopleCountElement =
                    (document.RootElement.TryGetProperty("incident", out var incident)
                     && incident.TryGetProperty("people_count", out peopleCount))
                    || document.RootElement.TryGetProperty("people_count", out peopleCount);

                if (!hasPeopleCountElement)
                {
                    continue;
                }

                hasPeopleCount = true;

                if (peopleCount.TryGetProperty("adult", out var adult) && adult.ValueKind == JsonValueKind.Number)
                {
                    totalAdult += adult.GetInt32();
                }

                if (peopleCount.TryGetProperty("child", out var child) && child.ValueKind == JsonValueKind.Number)
                {
                    totalChild += child.GetInt32();
                }

                if (peopleCount.TryGetProperty("elderly", out var elderly) && elderly.ValueKind == JsonValueKind.Number)
                {
                    totalElderly += elderly.GetInt32();
                }
            }
            catch (JsonException)
            {
                // Ignore malformed structured data when computing cluster aggregate.
            }
        }

        return (hasPeopleCount, totalAdult, totalChild, totalElderly);
    }
}
