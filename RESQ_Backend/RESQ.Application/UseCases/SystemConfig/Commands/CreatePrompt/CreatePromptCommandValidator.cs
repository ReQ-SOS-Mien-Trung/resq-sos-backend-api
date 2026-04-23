using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptCommandValidator : AbstractValidator<CreatePromptCommand>
{
    public CreatePromptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên prompt không được để trống.")
            .MaximumLength(255).WithMessage("Tên prompt không được vượt quá 255 ký tự.");

        RuleFor(x => x.PromptType)
            .IsInEnum().WithMessage("Loại prompt (PromptType) không hợp lệ.");

        RuleFor(x => x.PromptType)
            .NotEqual(PromptType.MissionPlanning)
            .WithMessage("Prompt type 'MissionPlanning' da bi ngung ho tro. Hay su dung cac prompt stage trong pipeline.");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Mục đích không được để trống.");

        RuleFor(x => x.SystemPrompt)
            .NotEmpty().WithMessage("System prompt không được để trống.");

        RuleFor(x => x.UserPromptTemplate)
            .NotEmpty().WithMessage("User prompt template không được để trống.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phiên bản không được để trống.")
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
            .Must(version => !PromptLifecycleStatusResolver.IsDraftVersion(version))
            .WithMessage("Version tạo mới không được dùng định dạng draft '-D'. Hãy dùng endpoint tạo draft.");
    }
}
