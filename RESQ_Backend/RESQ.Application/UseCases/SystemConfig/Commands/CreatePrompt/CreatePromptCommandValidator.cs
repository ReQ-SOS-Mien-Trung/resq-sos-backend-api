using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptCommandValidator : AbstractValidator<CreatePromptCommand>
{
    public CreatePromptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ten prompt khong duoc de trong.")
            .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.");

        RuleFor(x => x.PromptType)
            .IsInEnum().WithMessage("Loai prompt (PromptType) khong hop le.");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Muc dich khong duoc de trong.");

        RuleFor(x => x.SystemPrompt)
            .NotEmpty().WithMessage("System prompt khong duoc de trong.");

        RuleFor(x => x.UserPromptTemplate)
            .NotEmpty().WithMessage("User prompt template khong duoc de trong.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phien ban khong duoc de trong.")
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .Must(version => !PromptLifecycleStatusResolver.IsDraftVersion(version))
            .WithMessage("Version tao moi khong duoc dung dinh dang draft '-D'. Hay dung endpoint tao draft.");
    }
}
