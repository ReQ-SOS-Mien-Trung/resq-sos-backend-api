using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;

public class UpdateMissionActivityCommandValidator : AbstractValidator<UpdateMissionActivityCommand>
{
    public UpdateMissionActivityCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0");

        RuleFor(x => x.ActivityType)
            .MaximumLength(50).WithMessage("ActivityType không được vượt quá 50 ký tự")
            .When(x => x.ActivityType != null);

        RuleFor(x => x.Step)
            .GreaterThan(0).WithMessage("Step phải lớn hơn 0")
            .When(x => x.Step.HasValue);

        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("AssemblyPointId phải lớn hơn 0")
            .When(x => x.AssemblyPointId.HasValue);

        RuleFor(x => x)
            .Must(x => !x.AssemblyPointId.HasValue || (!x.TargetLatitude.HasValue && !x.TargetLongitude.HasValue))
            .WithMessage("Không được gửi TargetLatitude/TargetLongitude khi cập nhật AssemblyPointId.");
    }
}
