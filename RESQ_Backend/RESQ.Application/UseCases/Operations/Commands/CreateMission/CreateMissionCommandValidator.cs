using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId phải lớn hơn 0");

        RuleFor(x => x.CreatedById)
            .NotEmpty().WithMessage("CreatedById không được để trống");

        RuleFor(x => x.MissionType)
            .MaximumLength(50).WithMessage("MissionType không được vượt quá 50 ký tự")
            .When(x => x.MissionType != null);

        RuleFor(x => x.Activities)
            .NotNull().WithMessage("Danh sách activities không được null");

        RuleForEach(x => x.Activities).ChildRules(activity =>
        {
            activity.RuleFor(a => a.Step)
                .GreaterThan(0).WithMessage("Step phải lớn hơn 0")
                .When(a => a.Step.HasValue);

            activity.RuleFor(a => a.ActivityType)
                .NotEmpty().WithMessage("ActivityType không được để trống");
        });
    }
}
