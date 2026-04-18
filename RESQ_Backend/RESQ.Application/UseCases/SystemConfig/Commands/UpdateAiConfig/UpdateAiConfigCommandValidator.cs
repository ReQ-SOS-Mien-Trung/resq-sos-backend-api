using FluentValidation;
using RESQ.Application.Common;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;

public class UpdateAiConfigCommandValidator : AbstractValidator<UpdateAiConfigCommand>
{
    public UpdateAiConfigCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id AI config không hợp lệ.");

        RuleFor(x => x.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Tên AI config không được để trống.")
            .MaximumLength(255).WithMessage("Tên AI config không được vượt quá 255 ký tự.")
            .When(x => x.Name != null);

        RuleFor(x => x.Provider)
            .Must(provider => !provider.HasValue || Enum.IsDefined(typeof(AiProvider), provider.Value))
            .WithMessage("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".");

        RuleFor(x => x.Model)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Model không được để trống.")
            .MaximumLength(100).WithMessage("Model không được vượt quá 100 ký tự.")
            .When(x => x.Model != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0d, 2d).WithMessage("Temperature phải nằm trong khoảng từ 0 đến 2.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("MaxTokens phải lớn hơn 0.")
            .When(x => x.MaxTokens.HasValue);

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
            .When(x => x.Version != null);

        RuleFor(x => x.Version)
            .Must(v => v == null || PromptLifecycleStatusResolver.IsDraftVersion(v))
            .WithMessage("Version của draft phải chứa dấu hiệu '-D'.")
            .When(x => x.Version != null);
    }
}
