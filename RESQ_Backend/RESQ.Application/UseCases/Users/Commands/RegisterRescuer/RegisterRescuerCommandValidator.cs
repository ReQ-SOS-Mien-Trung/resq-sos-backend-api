using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandValidator : AbstractValidator<RegisterRescuerCommand>
    {
        public RegisterRescuerCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters")
                .MaximumLength(100).WithMessage("Password must not exceed 100 characters")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one number");

            RuleFor(x => x.FullName)
                .MaximumLength(255).WithMessage("Full name must not exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.FullName));
        }
    }
}
