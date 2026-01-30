using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Phone is required")
                .Matches(@"^\d{10,15}$").WithMessage("Phone must be between 10-15 digits");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .Length(6).WithMessage("Password must be exactly 6 digits")
                .Matches(@"^\d{6}$").WithMessage("Password must be a 6-digit PIN");
        }
    }
}
