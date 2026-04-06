using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionRouteCoordinateResolver
{
    public static bool HasUsableTargetCoordinates(MissionActivityModel activity) =>
        activity.TargetLatitude.HasValue
        && activity.TargetLongitude.HasValue
        && !IsZeroCoordinate(activity.TargetLatitude.Value, activity.TargetLongitude.Value);

    public static bool RequiresDepotFallback(MissionActivityModel activity) =>
        string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        && activity.DepotId.HasValue
        && !HasUsableTargetCoordinates(activity);

    public static async Task<(double Latitude, double Longitude)?> ResolveAsync(
        MissionActivityModel activity,
        IDepotRepository depotRepository,
        CancellationToken cancellationToken)
    {
        if (HasUsableTargetCoordinates(activity))
            return (activity.TargetLatitude!.Value, activity.TargetLongitude!.Value);

        if (!RequiresDepotFallback(activity))
            return null;

        var depot = await depotRepository.GetByIdAsync(activity.DepotId!.Value, cancellationToken);
        if (depot?.Location is null)
            return null;

        return (depot.Location.Latitude, depot.Location.Longitude);
    }

    private static bool IsZeroCoordinate(double latitude, double longitude) =>
        latitude == 0d && longitude == 0d;
}