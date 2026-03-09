using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;

public class AdminCreateUserCommandValidator : AbstractValidator<AdminCreateUserCommand>
{
    public AdminCreateUserCommandValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("RoleId không hợp lệ");

        When(x => !string.IsNullOrEmpty(x.Phone), () =>
        {
            RuleFor(x => x.Phone)
                .Matches(@"^(0|\+84)[3-9]\d{8}$").WithMessage("Số điện thoại không hợp lệ");
        });

        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email không hợp lệ");
        });

        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.Phone) || !string.IsNullOrEmpty(x.Email))
            .WithMessage("Phải cung cấp ít nhất số điện thoại hoặc email");
    }
}
