using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandValidator : AbstractValidator<UpsertWarningBandConfigCommand>
{
    public UpsertWarningBandConfigCommandValidator()
    {
        RuleFor(x => x.Bands)
            .NotNull().WithMessage("Bands không được null.")
            .NotEmpty().WithMessage("Phải có ít nhất một band.");

        RuleForEach(x => x.Bands).ChildRules(band =>
        {
            band.RuleFor(b => b.Name)
                .NotEmpty().WithMessage("Tên band không được rỗng.");

            band.RuleFor(b => b.From)
                .GreaterThanOrEqualTo(0m).WithMessage("From phải >= 0.");

            band.RuleFor(b => b.To)
                .GreaterThan(b => b.From)
                .When(b => b.To.HasValue)
                .WithMessage("To phải > From.");
        });
    }
}
