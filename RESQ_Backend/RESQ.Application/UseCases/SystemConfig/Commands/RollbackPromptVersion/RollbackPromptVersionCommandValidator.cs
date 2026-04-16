using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;

public class RollbackPromptVersionCommandValidator : AbstractValidator<RollbackPromptVersionCommand>
{
    public RollbackPromptVersionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id prompt khong hop le.");
    }
}
