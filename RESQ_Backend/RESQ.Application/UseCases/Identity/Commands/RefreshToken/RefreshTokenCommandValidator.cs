using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.RefreshToken
{
    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Access token l├á bß║»t buß╗Öc");
            RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token l├á bß║»t buß╗Öc");
        }
    }
}