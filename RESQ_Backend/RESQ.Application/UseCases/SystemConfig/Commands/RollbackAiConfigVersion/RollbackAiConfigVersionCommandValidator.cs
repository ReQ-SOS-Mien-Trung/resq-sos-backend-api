using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackAiConfigVersion;

public class RollbackAiConfigVersionCommandValidator : AbstractValidator<RollbackAiConfigVersionCommand>
{
    public RollbackAiConfigVersionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id AI config không hợp lệ.");
    }
}
