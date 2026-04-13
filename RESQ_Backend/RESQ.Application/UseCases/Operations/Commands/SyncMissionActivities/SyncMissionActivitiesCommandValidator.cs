using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public class SyncMissionActivitiesCommandValidator : AbstractValidator<SyncMissionActivitiesCommand>
{
    public SyncMissionActivitiesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không được để trống");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items không được để trống")
            .Must(items => items is { Count: >= 1 and <= 100 })
            .WithMessage("Items phải có từ 1 đến 100 phần tử");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ClientMutationId)
                    .NotEmpty().WithMessage("ClientMutationId không được để trống");

                item.RuleFor(x => x.MissionId)
                    .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

                item.RuleFor(x => x.ActivityId)
                    .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0");

                item.RuleFor(x => x.TargetStatus)
                    .IsInEnum().WithMessage("TargetStatus không hợp lệ");

                item.RuleFor(x => x.BaseServerStatus)
                    .IsInEnum().WithMessage("BaseServerStatus không hợp lệ");

                item.RuleFor(x => x.QueuedAt)
                    .NotEqual(default(DateTimeOffset)).WithMessage("QueuedAt không hợp lệ");
            });

        RuleFor(x => x.Items)
            .Must(HaveUniqueClientMutationIds)
            .WithMessage("Items có clientMutationId bị trùng lặp trong cùng request");

        RuleFor(x => x.Items)
            .Must(HaveUniqueMissionActivityPairs)
            .WithMessage("Items có cặp missionId/activityId bị trùng lặp trong cùng request");
    }

    private static bool HaveUniqueClientMutationIds(IReadOnlyList<MissionActivitySyncItemDto>? items) =>
        items is null || items.Count == items.Select(item => item.ClientMutationId).Distinct().Count();

    private static bool HaveUniqueMissionActivityPairs(IReadOnlyList<MissionActivitySyncItemDto>? items) =>
        items is null || items.Count == items.Select(item => (item.MissionId, item.ActivityId)).Distinct().Count();
}
