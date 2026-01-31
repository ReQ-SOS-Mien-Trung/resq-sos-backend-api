using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandValidator : AbstractValidator<RegisterRescuerCommand>
    {
        public RegisterRescuerCommandValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters")
                .MaximumLength(100).WithMessage("Username must not exceed 100 characters")
                .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters")
                .MaximumLength(100).WithMessage("Password must not exceed 100 characters");
        }
    }
}
