using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;

public class UpsertSosClusterGroupingConfigValidator : AbstractValidator<UpsertSosClusterGroupingConfigCommand>
{
    public UpsertSosClusterGroupingConfigValidator()
    {
        RuleFor(x => x.MaximumDistanceKm)
            .GreaterThan(0)
            .WithMessage("maximumDistanceKm phải lớn hơn 0.");
    }
}