using FluentValidation;
using RESQ.Application.Common;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;

public class CreateAiConfigCommandValidator : AbstractValidator<CreateAiConfigCommand>
{
    public CreateAiConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên AI config không được để trống.")
            .MaximumLength(255).WithMessage("Tên AI config không được vượt quá 255 ký tự.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model không được để trống.")
            .MaximumLength(100).WithMessage("Model không được vượt quá 100 ký tự.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0d, 2d).WithMessage("Temperature phải nằm trong khoảng từ 0 đến 2.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("MaxTokens phải lớn hơn 0.");

        RuleFor(x => x.ApiUrl)
            .NotEmpty().WithMessage("ApiUrl không được để trống.")
            .MaximumLength(500).WithMessage("ApiUrl không được vượt quá 500 ký tự.")
            .Must(BeAbsoluteUri).WithMessage("ApiUrl phải là URL tuyệt đối hợp lệ.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Phiên bản không được để trống.")
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
            .Must(version => !PromptLifecycleStatusResolver.IsDraftVersion(version))
            .WithMessage("Version tạo mới không được dùng định dạng draft '-D'. Hãy dùng endpoint tạo draft.");
    }

    private static bool BeAbsoluteUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
