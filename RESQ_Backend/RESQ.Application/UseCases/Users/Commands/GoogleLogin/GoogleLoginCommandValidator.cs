using FluentValidation;

namespace RESQ.Application.UseCases.Users.Commands.GoogleLogin
{
    public class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
    {
        public GoogleLoginCommandValidator()
        {
            RuleFor(x => x.IdToken)
                .NotEmpty().WithMessage("Token Google ID là bắt buộc");
        }
    }
}
