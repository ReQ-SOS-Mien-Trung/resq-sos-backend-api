using FluentValidation;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandValidator : AbstractValidator<UpsertWarningBandConfigCommand>
{
    public UpsertWarningBandConfigCommandValidator()
    {
        RuleFor(x => x.Bands)
            .NotNull().WithMessage("Bands khong duoc null.")
            .NotEmpty().WithMessage("Phai co it nhat mot band.")
            .Custom((bands, context) =>
            {
                if (bands == null)
                    return;

                var domainBands = bands
                    .Select(b => new WarningBand(b.Name, b.From, b.To))
                    .ToList();

                var validationError = WarningBandSet.ValidateFixedBandDefinition(domainBands);
                if (validationError != null)
                    context.AddFailure(validationError);
            });

        RuleForEach(x => x.Bands).ChildRules(band =>
        {
            band.RuleFor(b => b.Name)
                .NotEmpty().WithMessage("Ten band khong duoc rong.");

            band.RuleFor(b => b.From)
                .GreaterThanOrEqualTo(0m).WithMessage("From phai >= 0.");

            band.RuleFor(b => b.To)
                .GreaterThan(b => b.From)
                .When(b => b.To.HasValue)
                .WithMessage("To phai > From.");
        });
    }
}
