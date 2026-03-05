using FluentValidation;
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

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Mục đích không được để trống.");

        RuleFor(x => x.SystemPrompt)
            .NotEmpty().WithMessage("System prompt không được để trống.");

        RuleFor(x => x.UserPromptTemplate)
            .NotEmpty().WithMessage("User prompt template không được để trống.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Tên model AI không được để trống.")
            .MaximumLength(100).WithMessage("Tên model không được vượt quá 100 ký tự.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0, 2).WithMessage("Temperature phải nằm trong khoảng từ 0 đến 2.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("Max tokens phải lớn hơn 0.")
            .LessThanOrEqualTo(100000).WithMessage("Max tokens không được vượt quá 100000.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phiên bản không được để trống.")
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.");

        RuleFor(x => x.ApiUrl)
            .MaximumLength(500).WithMessage("API URL không được vượt quá 500 ký tự.")
            .When(x => x.ApiUrl != null);
    }
}
