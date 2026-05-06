using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateUserAbilities
{
    public class UpdateUserAbilitiesCommandValidator : AbstractValidator<UpdateUserAbilitiesCommand>
    {
        public UpdateUserAbilitiesCommandValidator()
        {
            RuleFor(x => x.CallerUserId)
                .NotEmpty().WithMessage("CallerUserId là bắt buộc");

            RuleFor(x => x.TargetUserId)
                .NotEmpty().WithMessage("TargetUserId là bắt buộc");

            RuleFor(x => x.Abilities)
                .NotNull().WithMessage("Danh sách abilities không được null");

            RuleForEach(x => x.Abilities).ChildRules(ability =>
            {
                ability.RuleFor(a => a.AbilityId)
                    .GreaterThan(0).WithMessage("AbilityId phải lớn hơn 0");
            });
        }
    }
}
