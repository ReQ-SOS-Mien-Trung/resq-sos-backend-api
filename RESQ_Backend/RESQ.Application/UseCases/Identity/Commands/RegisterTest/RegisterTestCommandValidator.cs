using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.RegisterTest
{
    public class RegisterTestCommandValidator : AbstractValidator<RegisterTestCommand>
    {
        public RegisterTestCommandValidator()
        {
            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Số điện thoại là bắt buộc")
                .Matches(@"^(0|\+84)[3-9]\d{8}$")
                .WithMessage("Số điện thoại phải là số điện thoại Việt Nam hợp lệ (VD: 0912345678 hoặc +84912345678)");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
                .Length(6).WithMessage("Mật khẩu phải đúng 6 chữ số")
                .Matches(@"^\d{6}$").WithMessage("Mật khẩu phải là mã PIN 6 chữ số");
        }
    }
}
