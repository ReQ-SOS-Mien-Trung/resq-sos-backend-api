using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.RescuerConsent
{
    public class RescuerConsentCommandValidator : AbstractValidator<RescuerConsentCommand>
    {
        public RescuerConsentCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId là bắt buộc");
        }
    }
}
