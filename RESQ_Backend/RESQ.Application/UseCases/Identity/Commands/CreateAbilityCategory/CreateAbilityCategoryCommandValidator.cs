using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.CreateAbilityCategory;

public class CreateAbilityCategoryCommandValidator : AbstractValidator<CreateAbilityCategoryCommand>
{
    public CreateAbilityCategoryCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code không được để trống.")
            .MaximumLength(50).WithMessage("Code tối đa 50 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Mô tả tối đa 255 ký tự.")
            .When(x => x.Description is not null);
    }
}
