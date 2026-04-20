using FluentValidation;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public class GetSosClustersQueryValidator : AbstractValidator<GetSosClustersQuery>
{
    public GetSosClustersQueryValidator()
    {
        RuleForEach(x => x.Statuses)
            .Must(BeValidStatus)
            .WithMessage("statuses contains an invalid SOS cluster status.");
    }

    private static bool BeValidStatus(string? status)
        => !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<SosClusterStatus>(status.Trim(), ignoreCase: true, out _);
}
