using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
        }
    }
}
