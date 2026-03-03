using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandValidator : AbstractValidator<UpdateActivityStatusCommand>
{
    private static readonly string[] AllowedStatuses = ["pending", "in_progress", "completed", "cancelled", "skipped"];

    public UpdateActivityStatusCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status không được để trống")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status phải là một trong: {string.Join(", ", AllowedStatuses)}");

        RuleFor(x => x.DecisionBy)
            .NotEmpty().WithMessage("DecisionBy không được để trống");
    }
}
