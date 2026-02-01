using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.VerifyEmail
{
    public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
    {
        public VerifyEmailCommandValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Mã xác minh là bắt buộc");
        }
    }
}
