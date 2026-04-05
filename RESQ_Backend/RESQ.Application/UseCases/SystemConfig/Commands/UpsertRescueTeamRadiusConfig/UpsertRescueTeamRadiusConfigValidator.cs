using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;

public class UpsertRescueTeamRadiusConfigValidator : AbstractValidator<UpsertRescueTeamRadiusConfigCommand>
{
    public UpsertRescueTeamRadiusConfigValidator()
    {
        RuleFor(x => x.MaxRadiusKm)
            .GreaterThan(0)
            .WithMessage("maxRadiusKm phải lớn hơn 0.");
    }
}
