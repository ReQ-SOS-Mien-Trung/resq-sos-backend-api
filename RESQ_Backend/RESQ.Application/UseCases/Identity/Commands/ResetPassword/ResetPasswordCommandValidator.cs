using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.ResetPassword
{
    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Token đặt lại mật khẩu là bắt buộc");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Mật khẩu mới là bắt buộc")
                .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Xác nhận mật khẩu là bắt buộc")
                .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu không khớp với mật khẩu mới");
        }
    }
}
