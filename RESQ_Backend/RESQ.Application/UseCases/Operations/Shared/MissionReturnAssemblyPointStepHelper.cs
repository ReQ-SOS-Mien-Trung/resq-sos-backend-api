using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionReturnAssemblyPointStepHelper
{
    public const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";

    public static int ReserveStepBeforeReturnAssemblyPoint(
        IReadOnlyCollection<MissionActivityModel> activities,
        int? missionTeamId,
        out List<MissionActivityModel> shiftedActivities)
    {
        shiftedActivities = [];

        var sameTeamReturnAssembly = missionTeamId.HasValue
            ? activities
                .Where(activity => activity.MissionTeamId == missionTeamId
                    && string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase)
                    && activity.Step.HasValue)
                .OrderBy(activity => activity.Step!.Value)
                .ThenBy(activity => activity.Id)
                .FirstOrDefault()
            : null;

        if (sameTeamReturnAssembly is null)
            return activities.Any() ? activities.Max(activity => activity.Step ?? 0) + 1 : 1;

        var insertionStep = sameTeamReturnAssembly.Step!.Value;
        shiftedActivities = activities
            .Where(activity => activity.MissionTeamId == missionTeamId
                && activity.Step.HasValue
                && activity.Step.Value >= insertionStep)
            .OrderByDescending(activity => activity.Step!.Value)
            .ThenByDescending(activity => activity.Id)
            .ToList();

        foreach (var activity in shiftedActivities)
            activity.Step = activity.Step!.Value + 1;

        return insertionStep;
    }
}
