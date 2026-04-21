using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.VerifyEmail
{
    public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
    {
        public VerifyEmailCommandValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("M├ú x├íc minh l├á bß║»t buß╗Öc");
        }
    }
}