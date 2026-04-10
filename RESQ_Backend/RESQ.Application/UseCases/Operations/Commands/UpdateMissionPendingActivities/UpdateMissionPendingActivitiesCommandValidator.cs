using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionPendingActivities;

public class UpdateMissionPendingActivitiesCommandValidator : AbstractValidator<UpdateMissionPendingActivitiesCommand>
{
    public UpdateMissionPendingActivitiesCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy là bắt buộc");

        RuleFor(x => x.Activities)
            .NotEmpty().WithMessage("Phải có ít nhất một activity để cập nhật");

        RuleFor(x => x.Activities)
            .Must(HaveDistinctActivityIds)
            .WithMessage("Danh sách activity không được chứa ActivityId trùng nhau");

        RuleForEach(x => x.Activities)
            .SetValidator(new UpdateMissionPendingActivityPatchValidator());
    }

    private static bool HaveDistinctActivityIds(IReadOnlyList<UpdateMissionPendingActivityPatch>? activities) =>
        activities is not null
        && activities.Select(activity => activity.ActivityId).Distinct().Count() == activities.Count;
}

internal class UpdateMissionPendingActivityPatchValidator : AbstractValidator<UpdateMissionPendingActivityPatch>
{
    public UpdateMissionPendingActivityPatchValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0");

        RuleFor(x => x.Step)
            .GreaterThan(0).WithMessage("Step phải lớn hơn 0")
            .When(x => x.Step.HasValue);

        RuleFor(x => x)
            .Must(HasAtLeastOneChange)
            .WithMessage("Mỗi activity phải có ít nhất một thay đổi");

        RuleFor(x => x)
            .Must(HaveCompleteCoordinates)
            .WithMessage("TargetLatitude và TargetLongitude phải được gửi cùng nhau");

        RuleFor(x => x.Items)
            .Must(HaveDistinctItemIds)
            .WithMessage("Danh sách item không được chứa ItemId trùng nhau")
            .When(x => x.Items is not null);

        RuleForEach(x => x.Items!)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ItemId)
                    .NotNull().WithMessage("ItemId là bắt buộc khi cập nhật item")
                    .GreaterThan(0).WithMessage("ItemId phải lớn hơn 0");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity phải lớn hơn 0");

                item.RuleFor(x => x.BufferRatio)
                    .GreaterThanOrEqualTo(0).WithMessage("BufferRatio không được âm")
                    .When(x => x.BufferRatio.HasValue);
            })
            .When(x => x.Items is not null);
    }

    private static bool HasAtLeastOneChange(UpdateMissionPendingActivityPatch patch) =>
        patch.Step.HasValue
        || patch.Description is not null
        || patch.Target is not null
        || patch.TargetLatitude.HasValue
        || patch.TargetLongitude.HasValue
        || patch.Items is not null;

    private static bool HaveCompleteCoordinates(UpdateMissionPendingActivityPatch patch) =>
        patch.TargetLatitude.HasValue == patch.TargetLongitude.HasValue;

    private static bool HaveDistinctItemIds(List<RESQ.Application.Services.SupplyToCollectDto>? items)
    {
        if (items is null)
            return true;

        var ids = items.Select(item => item.ItemId).ToList();
        return ids.Distinct().Count() == ids.Count;
    }
}