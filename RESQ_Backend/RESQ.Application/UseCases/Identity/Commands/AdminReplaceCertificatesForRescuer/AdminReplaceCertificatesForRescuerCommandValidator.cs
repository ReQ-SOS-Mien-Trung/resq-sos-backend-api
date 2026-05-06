using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.AdminReplaceCertificatesForRescuer
{
    public class AdminReplaceCertificatesForRescuerCommandValidator : AbstractValidator<AdminReplaceCertificatesForRescuerCommand>
    {
        public AdminReplaceCertificatesForRescuerCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Documents).NotNull().Must(d => d.Count > 0).Must(d => d.Count <= 10);
            RuleForEach(x => x.Documents).ChildRules(doc =>
            {
                doc.RuleFor(d => d.FileUrl).NotEmpty().MaximumLength(2000).Must(url => Uri.TryCreate(url, UriKind.Absolute, out _));
                doc.RuleFor(d => d.FileTypeId).GreaterThan(0);
            });
        }
    }
}
