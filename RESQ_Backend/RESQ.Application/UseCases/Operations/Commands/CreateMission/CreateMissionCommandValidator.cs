using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId phải lớn hơn 0");

        RuleFor(x => x.AiSuggestionId)
            .GreaterThan(0).WithMessage("AiSuggestionId phải lớn hơn 0")
            .When(x => x.AiSuggestionId.HasValue);

        RuleFor(x => x.CreatedById)
            .NotEmpty().WithMessage("CreatedById không được để trống");

        RuleFor(x => x.MissionType)
            .MaximumLength(50).WithMessage("MissionType không được vượt quá 50 ký tự")
            .When(x => x.MissionType != null);

        RuleFor(x => x.OverrideReason)
            .NotEmpty().WithMessage("OverrideReason không được để trống khi bỏ qua cảnh báo mixed mission")
            .When(x => x.IgnoreMixedMissionWarning);

        RuleFor(x => x.OverrideReason)
            .MaximumLength(1000).WithMessage("OverrideReason không được vượt quá 1000 ký tự")
            .When(x => x.OverrideReason != null);

        RuleFor(x => x.Activities)
            .NotNull().WithMessage("Danh sách activities không được null");

        RuleForEach(x => x.Activities).ChildRules(activity =>
        {
            activity.RuleFor(a => a.Step)
                .GreaterThan(0).WithMessage("Step phải lớn hơn 0")
                .When(a => a.Step.HasValue);

            activity.RuleFor(a => a.ActivityType)
                .NotEmpty().WithMessage("ActivityType không được để trống");

            activity.RuleFor(a => a.RescueTeamId)
                .NotNull().WithMessage("Mỗi activity phải được gán đội cứu hộ (RescueTeamId không được để trống)")
                .GreaterThan(0).WithMessage("RescueTeamId phải lớn hơn 0")
                .When(a => a.RescueTeamId.HasValue);
        });
    }
}
