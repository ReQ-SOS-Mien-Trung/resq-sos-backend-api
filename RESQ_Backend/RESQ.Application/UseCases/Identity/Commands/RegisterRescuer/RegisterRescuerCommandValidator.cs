using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandValidator : AbstractValidator<RegisterRescuerCommand>
    {
        public RegisterRescuerCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email l├á bß║»t buß╗Öc")
                .EmailAddress().WithMessage("─Éß╗ïnh dß║íng email kh├┤ng hß╗úp lß╗ç")
                .MaximumLength(255).WithMessage("Email kh├┤ng ─æ╞░ß╗úc v╞░ß╗út qu├í 255 k├╜ tß╗▒");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mß║¡t khß║⌐u l├á bß║»t buß╗Öc")
                .MinimumLength(6).WithMessage("Mß║¡t khß║⌐u phß║úi c├│ ├¡t nhß║Ñt 6 k├╜ tß╗▒")
                .MaximumLength(100).WithMessage("Mß║¡t khß║⌐u kh├┤ng ─æ╞░ß╗úc v╞░ß╗út qu├í 100 k├╜ tß╗▒")
                .Matches(@"[A-Z]").WithMessage("Mß║¡t khß║⌐u phß║úi chß╗⌐a ├¡t nhß║Ñt mß╗Öt chß╗» c├íi viß║┐t hoa")
                .Matches(@"[a-z]").WithMessage("Mß║¡t khß║⌐u phß║úi chß╗⌐a ├¡t nhß║Ñt mß╗Öt chß╗» c├íi viß║┐t th╞░ß╗¥ng")
                .Matches(@"[0-9]").WithMessage("Mß║¡t khß║⌐u phß║úi chß╗⌐a ├¡t nhß║Ñt mß╗Öt chß╗» sß╗æ");
        }
    }
}