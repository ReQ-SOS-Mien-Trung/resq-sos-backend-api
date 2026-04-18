using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePromptDraft;

public class CreatePromptDraftCommandValidator : AbstractValidator<CreatePromptDraftCommand>
{
    public CreatePromptDraftCommandValidator()
    {
        RuleFor(x => x.SourcePromptId)
            .GreaterThan(0).WithMessage("Id prompt nguồn không hợp lệ.");
    }
}
