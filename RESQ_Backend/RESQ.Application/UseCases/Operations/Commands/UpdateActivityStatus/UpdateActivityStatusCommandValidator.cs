using FluentValidation;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandValidator : AbstractValidator<UpdateActivityStatusCommand>
{
    public UpdateActivityStatusCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status phải là một trong: Planned, OnGoing, Succeed, Failed, Cancelled");
        RuleFor(x => x.DecisionBy)
            .NotEmpty().WithMessage("DecisionBy không được để trống");
    }
}
