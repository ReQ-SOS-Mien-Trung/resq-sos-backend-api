using FluentValidation;
using RESQ.Application.Common;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;

public class UpdateSosPriorityRuleConfigCommandValidator : AbstractValidator<UpdateSosPriorityRuleConfigCommand>
{
    public UpdateSosPriorityRuleConfigCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Config)
            .Custom((config, context) =>
            {
                foreach (var error in SosPriorityRuleConfigSupport.GetValidationErrors(config))
                {
                    context.AddFailure(nameof(UpdateSosPriorityRuleConfigCommand.Config), error);
                }
            });
    }
}