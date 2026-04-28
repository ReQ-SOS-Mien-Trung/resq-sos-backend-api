using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

public class GetRescuerOverviewQueryValidator : AbstractValidator<GetRescuerOverviewQuery>
{
    public GetRescuerOverviewQueryValidator()
    {
        RuleFor(x => x.Months)
            .InclusiveBetween(1, 24)
            .WithMessage("months must be between 1 and 24.");
    }
}
