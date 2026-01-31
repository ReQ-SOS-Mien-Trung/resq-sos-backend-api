using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.RefreshToken
{
    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Access token là bắt buộc");
            RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token là bắt buộc");
        }
    }
}
