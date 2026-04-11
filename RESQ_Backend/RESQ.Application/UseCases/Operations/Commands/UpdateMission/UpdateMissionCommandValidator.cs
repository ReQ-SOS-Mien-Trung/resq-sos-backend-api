using FluentValidation;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionCommandValidator : AbstractValidator<UpdateMissionCommand>
{
    public UpdateMissionCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId pháº£i lá»›n hÆ¡n 0");

        RuleFor(x => x.MissionType)
            .MaximumLength(50).WithMessage("MissionType khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 50 kÃ½ tá»±")
            .When(x => x.MissionType != null);

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy lÃ  báº¯t buá»™c khi cáº­p nháº­t activity")
            .When(x => x.Activities.Count > 0);

        RuleFor(x => x.Activities)
            .Must(HaveDistinctActivityIds)
            .WithMessage("Danh sÃ¡ch activity khÃ´ng Ä‘Æ°á»£c chá»©a ActivityId trÃ¹ng nhau")
            .When(x => x.Activities.Count > 0);

        RuleForEach(x => x.Activities)
            .SetValidator(new UpdateMissionActivityPatchValidator());
    }

    private static bool HaveDistinctActivityIds(IReadOnlyList<UpdateMissionActivityPatch> activities) =>
        activities.Select(activity => activity.ActivityId).Distinct().Count() == activities.Count;
}

internal class UpdateMissionActivityPatchValidator : AbstractValidator<UpdateMissionActivityPatch>
{
    public UpdateMissionActivityPatchValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId pháº£i lá»›n hÆ¡n 0");

        RuleFor(x => x.Step)
            .GreaterThan(0).WithMessage("Step pháº£i lá»›n hÆ¡n 0")
            .When(x => x.Step.HasValue);

        RuleFor(x => x)
            .Must(HasAtLeastOneChange)
            .WithMessage("Má»—i activity pháº£i cÃ³ Ã­t nháº¥t má»™t thay Ä‘á»•i");

        RuleFor(x => x)
            .Must(HaveCompleteCoordinates)
            .WithMessage("TargetLatitude vÃ  TargetLongitude pháº£i Ä‘Æ°á»£c gá»­i cÃ¹ng nhau");

        RuleFor(x => x.Items)
            .Must(HaveDistinctItemIds)
            .WithMessage("Danh sÃ¡ch item khÃ´ng Ä‘Æ°á»£c chá»©a ItemId trÃ¹ng nhau")
            .When(x => x.Items is not null);

        RuleForEach(x => x.Items!)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ItemId)
                    .NotNull().WithMessage("ItemId lÃ  báº¯t buá»™c khi cáº­p nháº­t item")
                    .GreaterThan(0).WithMessage("ItemId pháº£i lá»›n hÆ¡n 0");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity pháº£i lá»›n hÆ¡n 0");

                item.RuleFor(x => x.BufferRatio)
                    .GreaterThanOrEqualTo(0).WithMessage("BufferRatio khÃ´ng Ä‘Æ°á»£c Ã¢m")
                    .When(x => x.BufferRatio.HasValue);
            })
            .When(x => x.Items is not null);
    }

    private static bool HasAtLeastOneChange(UpdateMissionActivityPatch patch) =>
        patch.Step.HasValue
        || patch.Description is not null
        || patch.Target is not null
        || patch.TargetLatitude.HasValue
        || patch.TargetLongitude.HasValue
        || patch.Items is not null;

    private static bool HaveCompleteCoordinates(UpdateMissionActivityPatch patch) =>
        patch.TargetLatitude.HasValue == patch.TargetLongitude.HasValue;

    private static bool HaveDistinctItemIds(List<SupplyToCollectDto>? items)
    {
        if (items is null)
            return true;

        var ids = items.Select(item => item.ItemId).ToList();
        return ids.Distinct().Count() == ids.Count;
    }
}
