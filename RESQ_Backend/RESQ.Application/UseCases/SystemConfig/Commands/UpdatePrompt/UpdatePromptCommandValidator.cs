using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    private const string ProviderValidationMessage = "Provider khong hop le. Gia tri hop le: \"Gemini\" hoac \"OpenRouter\".";

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

        RuleFor(x => x.Provider)
            .Must(x => !x.HasValue || Enum.IsDefined(typeof(AiProvider), x.Value))
            .WithMessage(ProviderValidationMessage);

        RuleFor(x => x.Model)
            .MaximumLength(100).WithMessage("Ten model khong duoc vuot qua 100 ky tu.")
            .When(x => x.Model != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0, 2).WithMessage("Temperature phai nam trong khoang tu 0 den 2.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("Max tokens phai lon hon 0.")
            .LessThanOrEqualTo(100000).WithMessage("Max tokens khong duoc vuot qua 100000.")
            .When(x => x.MaxTokens.HasValue);

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .When(x => x.Version != null);

        RuleFor(x => x.Version)
            .Must(v => v == null || PromptLifecycleStatusResolver.IsDraftVersion(v))
            .WithMessage("Version cua draft phai chua dau hieu '-D'.")
            .When(x => x.Version != null);

        RuleFor(x => x.ApiUrl)
            .MaximumLength(500).WithMessage("API URL khong duoc vuot qua 500 ky tu.")
            .When(x => x.ApiUrl != null);
    }
}
