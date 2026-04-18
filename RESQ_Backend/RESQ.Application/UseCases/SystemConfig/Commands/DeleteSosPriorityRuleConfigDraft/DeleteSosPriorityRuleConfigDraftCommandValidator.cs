using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeleteSosPriorityRuleConfigDraft;

public class DeleteSosPriorityRuleConfigDraftCommandValidator : AbstractValidator<DeleteSosPriorityRuleConfigDraftCommand>
{
    public DeleteSosPriorityRuleConfigDraftCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
