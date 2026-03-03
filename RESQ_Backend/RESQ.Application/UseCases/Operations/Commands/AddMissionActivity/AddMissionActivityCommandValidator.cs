using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityCommandValidator : AbstractValidator<AddMissionActivityCommand>
{
    public AddMissionActivityCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

        RuleFor(x => x.ActivityType)
            .NotEmpty().WithMessage("ActivityType không được để trống")
            .MaximumLength(50).WithMessage("ActivityType không được vượt quá 50 ký tự");

        RuleFor(x => x.Step)
            .GreaterThan(0).WithMessage("Step phải lớn hơn 0")
            .When(x => x.Step.HasValue);
    }
}
