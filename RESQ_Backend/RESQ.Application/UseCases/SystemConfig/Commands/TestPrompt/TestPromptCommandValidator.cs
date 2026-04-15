using FluentValidation;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandValidator : AbstractValidator<TestPromptCommand>
{
    private const string ProviderValidationMessage = "Provider khong hop le. Gia tri hop le: \"Gemini\" hoac \"OpenRouter\".";

    public TestPromptCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId khong hop le.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Che do test prompt khong hop le.");

        When(x => x.Mode == TestPromptDraftMode.ExistingPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .NotNull().WithMessage("PromptId khong duoc de trong.")
                .GreaterThan(0).WithMessage("PromptId khong hop le.");

            RuleFor(x => x.Name)
                .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.")
                .When(x => x.Name != null);

            RuleFor(x => x.PromptType)
                .Must(x => !x.HasValue || Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loai prompt khong hop le.");

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

            RuleFor(x => x.ApiUrl)
                .MaximumLength(500).WithMessage("API URL khong duoc vuot qua 500 ky tu.")
                .When(x => x.ApiUrl != null);
        });

        When(x => x.Mode == TestPromptDraftMode.NewPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .Null().WithMessage("PromptId phai de trong khi test prompt moi.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Ten prompt khong duoc de trong.")
                .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.");

            RuleFor(x => x.PromptType)
                .NotNull().WithMessage("Loai prompt khong duoc de trong.")
                .Must(x => x.HasValue && Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loai prompt khong hop le.");

            RuleFor(x => x.Provider)
                .NotNull().WithMessage("Provider khong duoc de trong.")
                .Must(x => x.HasValue && Enum.IsDefined(typeof(AiProvider), x.Value))
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
                .NotNull().WithMessage("Temperature khong duoc de trong.")
                .InclusiveBetween(0, 2).WithMessage("Temperature phai nam trong khoang tu 0 den 2.");

            RuleFor(x => x.MaxTokens)
                .NotNull().WithMessage("Max tokens khong duoc de trong.")
                .GreaterThan(0).WithMessage("Max tokens phai lon hon 0.")
                .LessThanOrEqualTo(100000).WithMessage("Max tokens khong duoc vuot qua 100000.");

            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Phien ban khong duoc de trong.")
                .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.");

            RuleFor(x => x.ApiUrl)
                .MaximumLength(500).WithMessage("API URL khong duoc vuot qua 500 ky tu.")
                .When(x => x.ApiUrl != null);
        });
    }
}
