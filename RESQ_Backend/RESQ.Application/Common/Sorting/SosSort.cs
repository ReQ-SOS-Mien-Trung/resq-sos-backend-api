using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Common.Sorting;

public enum SosSortField
{
    Time,
    Severity
}

public enum SosSortDirection
{
    Asc,
    Desc
}

public sealed record SosSortOption(SosSortField Field, SosSortDirection Direction);

public static class SosSortParser
{
    private static readonly IReadOnlyList<SosSortOption> DefaultOptions =
    [
        new(SosSortField.Time, SosSortDirection.Desc)
    ];

    public static IReadOnlyList<SosSortOption> Normalize(IReadOnlyList<SosSortOption>? options)
        => options is { Count: > 0 } ? options : DefaultOptions;

    public static bool TryParse(
        string? sort,
        out IReadOnlyList<SosSortOption> options,
        out string? error)
    {
        options = DefaultOptions;
        error = null;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var tokens = sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            error = "Sort must contain at least one field:direction pair.";
            return false;
        }

        var parsedOptions = new List<SosSortOption>();
        var seenFields = new HashSet<SosSortField>();

        foreach (var token in tokens)
        {
            var parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                error = "Sort must use the format field:direction, for example severity:desc,time:desc.";
                return false;
            }

            if (!TryParseField(parts[0], out var field))
            {
                error = $"Unsupported sort field '{parts[0]}'. Supported fields are time and severity.";
                return false;
            }

            if (!TryParseDirection(parts[1], out var direction))
            {
                error = $"Unsupported sort direction '{parts[1]}'. Supported directions are asc and desc.";
                return false;
            }

            if (!seenFields.Add(field))
            {
                error = $"Duplicate sort field '{parts[0]}' is not allowed.";
                return false;
            }

            parsedOptions.Add(new SosSortOption(field, direction));
        }

        options = parsedOptions;
        return true;
    }

    public static IOrderedEnumerable<SosRequestModel> ApplyToRequests(
        IEnumerable<SosRequestModel> requests,
        IReadOnlyList<SosSortOption>? options)
    {
        IOrderedEnumerable<SosRequestModel>? ordered = null;

        foreach (var option in Normalize(options))
        {
            ordered = option.Field switch
            {
                SosSortField.Time => ApplyRequestTimeSort(requests, ordered, option.Direction),
                SosSortField.Severity => ApplyRequestSeveritySort(requests, ordered, option.Direction),
                _ => ordered
            };
        }

        return ApplyThenBy(ordered, requests, request => request.Id, descending: true);
    }

    public static IOrderedEnumerable<SosClusterModel> ApplyToClusters(
        IEnumerable<SosClusterModel> clusters,
        IReadOnlyList<SosSortOption>? options)
    {
        IOrderedEnumerable<SosClusterModel>? ordered = null;

        foreach (var option in Normalize(options))
        {
            ordered = option.Field switch
            {
                SosSortField.Time => ApplyClusterTimeSort(clusters, ordered, option.Direction),
                SosSortField.Severity => ApplyClusterSeveritySort(clusters, ordered, option.Direction),
                _ => ordered
            };
        }

        return ApplyThenBy(ordered, clusters, cluster => cluster.Id, descending: true);
    }

    public static int GetPriorityRank(SosPriorityLevel? priorityLevel) =>
        priorityLevel switch
        {
            SosPriorityLevel.Critical => 4,
            SosPriorityLevel.High => 3,
            SosPriorityLevel.Medium => 2,
            SosPriorityLevel.Low => 1,
            _ => 0
        };

    public static int GetSeverityRank(string? severity)
    {
        var normalized = severity?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        return normalized switch
        {
            var value when value.Equals("Critical", StringComparison.OrdinalIgnoreCase) => 4,
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Equals("Severe", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals("Moderate", StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => 1,
            var value when value.Equals("Minor", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };
    }

    private static bool TryParseField(string value, out SosSortField field)
    {
        if (value.Equals("time", StringComparison.OrdinalIgnoreCase))
        {
            field = SosSortField.Time;
            return true;
        }

        if (value.Equals("severity", StringComparison.OrdinalIgnoreCase))
        {
            field = SosSortField.Severity;
            return true;
        }

        field = default;
        return false;
    }

    private static bool TryParseDirection(string value, out SosSortDirection direction)
    {
        if (value.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SosSortDirection.Asc;
            return true;
        }

        if (value.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SosSortDirection.Desc;
            return true;
        }

        direction = default;
        return false;
    }

    private static IOrderedEnumerable<SosRequestModel> ApplyRequestTimeSort(
        IEnumerable<SosRequestModel> source,
        IOrderedEnumerable<SosRequestModel>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyThenBy(ordered, source, request => request.CreatedAt is null, descending: false);
        return ApplyThenBy(ordered, source, request => request.CreatedAt, direction == SosSortDirection.Desc);
    }

    private static IOrderedEnumerable<SosRequestModel> ApplyRequestSeveritySort(
        IEnumerable<SosRequestModel> source,
        IOrderedEnumerable<SosRequestModel>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyThenBy(ordered, source, request => GetPriorityRank(request.PriorityLevel) == 0, descending: false);
        return ApplyThenBy(ordered, source, request => GetPriorityRank(request.PriorityLevel), direction == SosSortDirection.Desc);
    }

    private static IOrderedEnumerable<SosClusterModel> ApplyClusterTimeSort(
        IEnumerable<SosClusterModel> source,
        IOrderedEnumerable<SosClusterModel>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyThenBy(ordered, source, cluster => cluster.CreatedAt is null, descending: false);
        return ApplyThenBy(ordered, source, cluster => cluster.CreatedAt, direction == SosSortDirection.Desc);
    }

    private static IOrderedEnumerable<SosClusterModel> ApplyClusterSeveritySort(
        IEnumerable<SosClusterModel> source,
        IOrderedEnumerable<SosClusterModel>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyThenBy(ordered, source, cluster => GetSeverityRank(cluster.SeverityLevel) == 0, descending: false);
        return ApplyThenBy(ordered, source, cluster => GetSeverityRank(cluster.SeverityLevel), direction == SosSortDirection.Desc);
    }

    private static IOrderedEnumerable<T> ApplyThenBy<T, TKey>(
        IOrderedEnumerable<T>? ordered,
        IEnumerable<T> source,
        Func<T, TKey> keySelector,
        bool descending)
        => ordered is null
            ? descending
                ? source.OrderByDescending(keySelector)
                : source.OrderBy(keySelector)
            : descending
                ? ordered.ThenByDescending(keySelector)
                : ordered.ThenBy(keySelector);
}
