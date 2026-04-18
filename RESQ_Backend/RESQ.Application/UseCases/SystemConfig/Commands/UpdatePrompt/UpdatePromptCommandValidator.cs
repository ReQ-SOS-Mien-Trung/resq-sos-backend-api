using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    public UpdatePromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id prompt không hợp lệ.");

        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Tên prompt không được vượt quá 255 ký tự.")
            .When(x => x.Name != null);

        RuleFor(x => x.PromptType)
            .Must(promptType => !promptType.HasValue || Enum.IsDefined(typeof(PromptType), promptType.Value))
            .WithMessage("Loại prompt (PromptType) không hợp lệ.");

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
            .When(x => x.Version != null);

        RuleFor(x => x.Version)
            .Must(v => v == null || PromptLifecycleStatusResolver.IsDraftVersion(v))
            .WithMessage("Version của draft phải chứa dấu hiệu '-D'.")
            .When(x => x.Version != null);

    }
}
