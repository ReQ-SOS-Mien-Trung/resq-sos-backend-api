using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail
{
    public class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
    {
        public ResendVerificationEmailCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email là bắt buộc")
                .EmailAddress().WithMessage("Định dạng email không hợp lệ");
        }
    }
}
