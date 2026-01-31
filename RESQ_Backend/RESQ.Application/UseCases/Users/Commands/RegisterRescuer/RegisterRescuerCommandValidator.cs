using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandValidator : AbstractValidator<RegisterRescuerCommand>
    {
        public RegisterRescuerCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email là bắt buộc")
                .EmailAddress().WithMessage("Định dạng email không hợp lệ")
                .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
                .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự")
                .MaximumLength(100).WithMessage("Mật khẩu không được vượt quá 100 ký tự")
                .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết hoa")
                .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết thường")
                .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất một chữ số");

            RuleFor(x => x.FullName)
                .MaximumLength(255).WithMessage("Họ tên không được vượt quá 255 ký tự")
                .When(x => !string.IsNullOrEmpty(x.FullName));
        }
    }
}
