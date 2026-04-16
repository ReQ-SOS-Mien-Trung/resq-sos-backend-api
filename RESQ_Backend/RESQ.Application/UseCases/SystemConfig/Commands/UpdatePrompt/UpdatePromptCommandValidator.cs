using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    public UpdatePromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id prompt khong hop le.");

        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.")
            .When(x => x.Name != null);

        RuleFor(x => x.PromptType)
            .Must(promptType => !promptType.HasValue || Enum.IsDefined(typeof(PromptType), promptType.Value))
            .WithMessage("Loai prompt (PromptType) khong hop le.");

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .When(x => x.Version != null);

        RuleFor(x => x.Version)
            .Must(v => v == null || PromptLifecycleStatusResolver.IsDraftVersion(v))
            .WithMessage("Version cua draft phai chua dau hieu '-D'.")
            .When(x => x.Version != null);

    }
}
