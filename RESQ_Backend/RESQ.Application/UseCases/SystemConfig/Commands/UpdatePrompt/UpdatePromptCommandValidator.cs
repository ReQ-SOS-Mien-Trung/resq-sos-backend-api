using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    public UpdatePromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id prompt không hợp lệ.");

        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Tên prompt không được vượt quá 255 ký tự.")
            .When(x => x.Name != null);

        RuleFor(x => x.Model)
            .MaximumLength(100).WithMessage("Tên model không được vượt quá 100 ký tự.")
            .When(x => x.Model != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0, 2).WithMessage("Temperature phải nằm trong khoảng từ 0 đến 2.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("Max tokens phải lớn hơn 0.")
            .LessThanOrEqualTo(100000).WithMessage("Max tokens không được vượt quá 100000.")
            .When(x => x.MaxTokens.HasValue);

        RuleFor(x => x.Version)
            .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
            .When(x => x.Version != null);

        RuleFor(x => x.ApiUrl)
            .MaximumLength(500).WithMessage("API URL không được vượt quá 500 ký tự.")
            .When(x => x.ApiUrl != null);
    }
}
