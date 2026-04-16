using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivatePromptVersion;

public class ActivatePromptVersionCommandValidator : AbstractValidator<ActivatePromptVersionCommand>
{
    public ActivatePromptVersionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id prompt khong hop le.");
    }
}
