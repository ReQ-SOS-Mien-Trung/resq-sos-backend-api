using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateSosPriorityRuleConfig;

public class ActivateSosPriorityRuleConfigCommandValidator : AbstractValidator<ActivateSosPriorityRuleConfigCommand>
{
    public ActivateSosPriorityRuleConfigCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
