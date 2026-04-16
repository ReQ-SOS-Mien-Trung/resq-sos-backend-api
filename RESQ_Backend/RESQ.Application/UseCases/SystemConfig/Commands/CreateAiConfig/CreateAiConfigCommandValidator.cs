using FluentValidation;
using RESQ.Application.Common;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;

public class CreateAiConfigCommandValidator : AbstractValidator<CreateAiConfigCommand>
{
    public CreateAiConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ten AI config khong duoc de trong.")
            .MaximumLength(255).WithMessage("Ten AI config khong duoc vuot qua 255 ky tu.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("Provider khong hop le. Gia tri hop le: \"Gemini\" hoac \"OpenRouter\".");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model khong duoc de trong.")
            .MaximumLength(100).WithMessage("Model khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0d, 2d).WithMessage("Temperature phai nam trong khoang tu 0 den 2.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("MaxTokens phai lon hon 0.");

        RuleFor(x => x.ApiUrl)
            .NotEmpty().WithMessage("ApiUrl khong duoc de trong.")
            .MaximumLength(500).WithMessage("ApiUrl khong duoc vuot qua 500 ky tu.")
            .Must(BeAbsoluteUri).WithMessage("ApiUrl phai la URL tuyet doi hop le.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phien ban khong duoc de trong.")
            .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
            .Must(version => !PromptLifecycleStatusResolver.IsDraftVersion(version))
            .WithMessage("Version tao moi khong duoc dung dinh dang draft '-D'. Hay dung endpoint tao draft.");
    }

    private static bool BeAbsoluteUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
