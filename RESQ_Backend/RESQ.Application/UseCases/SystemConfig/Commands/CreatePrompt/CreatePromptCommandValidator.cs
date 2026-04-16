using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptCommandValidator : AbstractValidator<CreatePromptCommand>
{
    private const string ProviderValidationMessage = "Provider khong hop le. Gia tri hop le: \"Gemini\" hoac \"OpenRouter\".";

    public CreatePromptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ten prompt khong duoc de trong.")
            .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.");

        RuleFor(x => x.PromptType)
            .IsInEnum().WithMessage("Loai prompt (PromptType) khong hop le.");

        RuleFor(x => x.Provider)
            .Must(provider => Enum.IsDefined(typeof(AiProvider), provider))
            .WithMessage(ProviderValidationMessage);

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Muc dich khong duoc de trong.");

        RuleFor(x => x.SystemPrompt)
            .NotEmpty().WithMessage("System prompt khong duoc de trong.");

        RuleFor(x => x.UserPromptTemplate)
            .NotEmpty().WithMessage("User prompt template khong duoc de trong.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Ten model AI khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ten model khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0, 2).WithMessage("Temperature phai nam trong khoang tu 0 den 2.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("Max tokens phai lon hon 0.")
            .LessThanOrEqualTo(100000).WithMessage("Max tokens khong duoc vuot qua 100000.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phien ban khong duoc de trong.")
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .Must(version => !PromptLifecycleStatusResolver.IsDraftVersion(version))
            .WithMessage("Version tao moi khong duoc dung dinh dang draft '-D'. Hay dung endpoint tao draft.");

        RuleFor(x => x.ApiUrl)
            .MaximumLength(500).WithMessage("API URL khong duoc vuot qua 500 ky tu.")
            .When(x => x.ApiUrl != null);
    }
}
