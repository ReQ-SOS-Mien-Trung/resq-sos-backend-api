using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfigDraft;

public class CreateAiConfigDraftCommandValidator : AbstractValidator<CreateAiConfigDraftCommand>
{
    public CreateAiConfigDraftCommandValidator()
    {
        RuleFor(x => x.SourceAiConfigId)
            .GreaterThan(0).WithMessage("Id AI config không hợp lệ.");
    }
}
