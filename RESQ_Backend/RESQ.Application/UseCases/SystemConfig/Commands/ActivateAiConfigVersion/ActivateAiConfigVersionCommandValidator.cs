using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateAiConfigVersion;

public class ActivateAiConfigVersionCommandValidator : AbstractValidator<ActivateAiConfigVersionCommand>
{
    public ActivateAiConfigVersionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id AI config khong hop le.");
    }
}
