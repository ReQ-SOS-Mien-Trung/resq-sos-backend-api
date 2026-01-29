using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.Login
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
        }
    }
}
