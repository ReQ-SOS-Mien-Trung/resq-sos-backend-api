using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId phai lon hon 0");

        RuleFor(x => x.AiSuggestionId)
            .GreaterThan(0).WithMessage("AiSuggestionId phai lon hon 0")
            .When(x => x.AiSuggestionId.HasValue);

        RuleFor(x => x.CreatedById)
            .NotEmpty().WithMessage("CreatedById khong duoc de trong");

        RuleFor(x => x.MissionType)
            .MaximumLength(50).WithMessage("MissionType khong duoc vuot qua 50 ky tu")
            .When(x => x.MissionType != null);

        RuleFor(x => x.OverrideReason)
            .NotEmpty().WithMessage("OverrideReason khong duoc de trong khi bo qua canh bao mixed mission")
            .When(x => x.IgnoreMixedMissionWarning);

        RuleFor(x => x.OverrideReason)
            .MaximumLength(1000).WithMessage("OverrideReason khong duoc vuot qua 1000 ky tu")
            .When(x => x.OverrideReason != null);

        RuleFor(x => x.Activities)
            .NotNull().WithMessage("Danh sach activities khong duoc null");

        RuleForEach(x => x.Activities).ChildRules(activity =>
        {
            activity.RuleFor(a => a.Step)
                .GreaterThan(0).WithMessage("Step phai lon hon 0")
                .When(a => a.Step.HasValue);

            activity.RuleFor(a => a.ActivityType)
                .NotEmpty().WithMessage("ActivityType khong duoc de trong");

            activity.RuleFor(a => a.RescueTeamId)
                .NotNull().WithMessage("Moi activity phai duoc gan doi cuu ho (RescueTeamId khong duoc de trong)")
                .GreaterThan(0).WithMessage("RescueTeamId phai lon hon 0")
                .When(a => a.RescueTeamId.HasValue);
        });
    }
}
