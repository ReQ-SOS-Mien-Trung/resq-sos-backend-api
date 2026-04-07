using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionRouteCoordinateResolver
{
    public static bool HasUsableCoordinates(double? latitude, double? longitude) =>
        latitude.HasValue
        && longitude.HasValue
        && !IsZeroCoordinate(latitude.Value, longitude.Value);

    public static bool HasUsableTargetCoordinates(MissionActivityModel activity) =>
        HasUsableCoordinates(activity.TargetLatitude, activity.TargetLongitude);

    public static bool UsesDepotCoordinates(MissionActivityModel activity) =>
        string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        && activity.DepotId.HasValue;

    public static bool RequiresDepotFallback(MissionActivityModel activity) =>
        UsesDepotCoordinates(activity)
        && !HasUsableTargetCoordinates(activity);

    public static async Task<(double Latitude, double Longitude)?> ResolveAsync(
        MissionActivityModel activity,
        IDepotRepository depotRepository,
        CancellationToken cancellationToken)
    {
        if (UsesDepotCoordinates(activity))
        {
            var depotCoordinates = await ResolveDepotCoordinatesAsync(activity, depotRepository, cancellationToken);
            if (depotCoordinates is not null)
                return depotCoordinates;
        }

        if (HasUsableTargetCoordinates(activity))
            return (activity.TargetLatitude!.Value, activity.TargetLongitude!.Value);

        return null;
    }

    private static async Task<(double Latitude, double Longitude)?> ResolveDepotCoordinatesAsync(
        MissionActivityModel activity,
        IDepotRepository depotRepository,
        CancellationToken cancellationToken)
    {
        if (!activity.DepotId.HasValue)
            return null;

        var depot = await depotRepository.GetByIdAsync(activity.DepotId!.Value, cancellationToken);
        if (depot?.Location is null)
            return null;

        return (depot.Location.Latitude, depot.Location.Longitude);
    }

    private static bool IsZeroCoordinate(double latitude, double longitude) =>
        latitude == 0d && longitude == 0d;
}