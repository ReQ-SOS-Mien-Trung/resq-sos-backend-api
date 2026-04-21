using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail
{
    public class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
    {
        public ResendVerificationEmailCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email l├á bß║»t buß╗Öc")
                .EmailAddress().WithMessage("─Éß╗ïnh dß║íng email kh├┤ng hß╗úp lß╗ç");
        }
    }
}