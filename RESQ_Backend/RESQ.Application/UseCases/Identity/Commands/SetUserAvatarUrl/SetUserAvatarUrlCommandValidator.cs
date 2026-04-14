using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl
{
    public class SetUserAvatarUrlCommandValidator : AbstractValidator<SetUserAvatarUrlCommand>
    {
        public SetUserAvatarUrlCommandValidator()
        {
            RuleFor(x => x.AvatarUrl).NotEmpty().WithMessage("AvatarUrl không du?c d? tr?ng");
        }
    }
}
