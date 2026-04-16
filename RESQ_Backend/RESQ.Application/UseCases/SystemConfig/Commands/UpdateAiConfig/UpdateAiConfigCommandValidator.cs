using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;

public class UpdateAiConfigCommandValidator : AbstractValidator<UpdateAiConfigCommand>
{
    public UpdateAiConfigCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id AI config khong hop le.");

        RuleFor(x => x.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Ten AI config khong duoc de trong.")
            .MaximumLength(255).WithMessage("Ten AI config khong duoc vuot qua 255 ky tu.")
            .When(x => x.Name != null);

        RuleFor(x => x.Provider)
            .Must(provider => !provider.HasValue || Enum.IsDefined(typeof(AiProvider), provider.Value))
            .WithMessage("Provider khong hop le. Gia tri hop le: \"Gemini\" hoac \"OpenRouter\".");

        RuleFor(x => x.Model)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Model khong duoc de trong.")
            .MaximumLength(100).WithMessage("Model khong duoc vuot qua 100 ky tu.")
            .When(x => x.Model != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0d, 2d).WithMessage("Temperature phai nam trong khoang tu 0 den 2.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("MaxTokens phai lon hon 0.")
            .When(x => x.MaxTokens.HasValue);

        RuleFor(x => x.ApiUrl)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("ApiUrl khong duoc de trong.")
            .MaximumLength(500).WithMessage("ApiUrl khong duoc vuot qua 500 ky tu.")
            .Must(BeAbsoluteUri).WithMessage("ApiUrl phai la URL tuyet doi hop le.")
            .When(x => x.ApiUrl != null);

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .When(x => x.Version != null);

        RuleFor(x => x.Version)
            .Must(v => v == null || PromptLifecycleStatusResolver.IsDraftVersion(v))
            .WithMessage("Version cua draft phai chua dau hieu '-D'.")
            .When(x => x.Version != null);
    }

    private static bool BeAbsoluteUri(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
