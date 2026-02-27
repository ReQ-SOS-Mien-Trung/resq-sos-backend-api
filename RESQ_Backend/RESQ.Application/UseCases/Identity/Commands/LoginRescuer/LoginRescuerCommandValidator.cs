using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.LoginRescuer
{
    public class LoginRescuerCommandValidator : AbstractValidator<LoginRescuerCommand>
    {
        public LoginRescuerCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống")
                .EmailAddress().WithMessage("Email không hợp lệ")
                .MaximumLength(255);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu không được để trống")
                .MaximumLength(100);
        }
    }
}
