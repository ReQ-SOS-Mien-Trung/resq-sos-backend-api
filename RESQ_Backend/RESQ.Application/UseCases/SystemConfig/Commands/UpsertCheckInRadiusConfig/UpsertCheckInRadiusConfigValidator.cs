using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;

public class UpsertCheckInRadiusConfigValidator : AbstractValidator<UpsertCheckInRadiusConfigCommand>
{
    public UpsertCheckInRadiusConfigValidator()
    {
        RuleFor(x => x.MaxRadiusMeters)
            .GreaterThan(0)
            .WithMessage("maxRadiusMeters phải lớn hơn 0.")
            .LessThanOrEqualTo(10_000)
            .WithMessage("maxRadiusMeters không được vượt quá 10.000m (10km).");
    }
}
