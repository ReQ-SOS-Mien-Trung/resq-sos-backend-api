using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;

public class SaveRescuerAbilitiesCommandValidator : AbstractValidator<SaveRescuerAbilitiesCommand>
{
    public SaveRescuerAbilitiesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(x => x.Abilities)
            .NotNull().WithMessage("Danh sách ability không được null.");

        RuleForEach(x => x.Abilities).ChildRules(ability =>
        {
            ability.RuleFor(a => a.AbilityId)
                .GreaterThan(0).WithMessage("AbilityId phải lớn hơn 0.");

            ability.RuleFor(a => a.Level)
                .InclusiveBetween(1, 10)
                .When(a => a.Level.HasValue)
                .WithMessage("Level phải nằm trong khoảng từ 1 đến 10.");
        });
    }
}
