using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;

public class UpsertRescuerScoreVisibilityConfigValidator : AbstractValidator<UpsertRescuerScoreVisibilityConfigCommand>
{
    public UpsertRescuerScoreVisibilityConfigValidator()
    {
        RuleFor(x => x.MinimumEvaluationCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("minimumEvaluationCount phải lớn hơn hoặc bằng 0.");
    }
}
