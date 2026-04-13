using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public class SyncMissionActivitiesCommandValidator : AbstractValidator<SyncMissionActivitiesCommand>
{
    public SyncMissionActivitiesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng")
            .Must(items => items is { Count: >= 1 and <= 100 })
            .WithMessage("Items pháº£i cÃ³ tá»« 1 Ä‘áº¿n 100 pháº§n tá»­");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ClientMutationId)
                    .NotEmpty().WithMessage("ClientMutationId khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");

                item.RuleFor(x => x.MissionId)
                    .GreaterThan(0).WithMessage("MissionId pháº£i lá»›n hÆ¡n 0");

                item.RuleFor(x => x.ActivityId)
                    .GreaterThan(0).WithMessage("ActivityId pháº£i lá»›n hÆ¡n 0");

                item.RuleFor(x => x.TargetStatus)
                    .IsInEnum().WithMessage("TargetStatus khÃ´ng há»£p lá»‡");

                item.RuleFor(x => x.BaseServerStatus)
                    .IsInEnum().WithMessage("BaseServerStatus khÃ´ng há»£p lá»‡");

                item.RuleFor(x => x.QueuedAt)
                    .NotEqual(default(DateTimeOffset)).WithMessage("QueuedAt khÃ´ng há»£p lá»‡");

                item.RuleFor(x => x.ImageUrl)
                    .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
                    .WithMessage("ImageUrl pháº£i lÃ  má»™t URL tuyá»‡t Ä‘á»‘i há»£p lá»‡.");
            });

        RuleFor(x => x.Items)
            .Must(HaveUniqueClientMutationIds)
            .WithMessage("Items cÃ³ clientMutationId bá»‹ trÃ¹ng láº·p trong cÃ¹ng request");

        RuleFor(x => x.Items)
            .Must(HaveUniqueMissionActivityPairs)
            .WithMessage("Items cÃ³ cáº·p missionId/activityId bá»‹ trÃ¹ng láº·p trong cÃ¹ng request");
    }

    private static bool HaveUniqueClientMutationIds(IReadOnlyList<MissionActivitySyncItemDto>? items) =>
        items is null || items.Count == items.Select(item => item.ClientMutationId).Distinct().Count();

    private static bool HaveUniqueMissionActivityPairs(IReadOnlyList<MissionActivitySyncItemDto>? items) =>
        items is null || items.Count == items.Select(item => (item.MissionId, item.ActivityId)).Distinct().Count();
}
