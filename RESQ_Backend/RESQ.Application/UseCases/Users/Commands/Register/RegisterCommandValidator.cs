using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Số điện thoại là bắt buộc")
                .Matches(@"^\d{10,15}$").WithMessage("Số điện thoại phải có từ 10-15 chữ số");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
                .Length(6).WithMessage("Mật khẩu phải đúng 6 chữ số")
                .Matches(@"^\d{6}$").WithMessage("Mật khẩu phải là mã PIN 6 chữ số");
        }
    }
}
