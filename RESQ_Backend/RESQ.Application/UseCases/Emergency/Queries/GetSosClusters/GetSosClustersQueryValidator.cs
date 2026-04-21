using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public class GetSosClustersQueryValidator : AbstractValidator<GetSosClustersQuery>
{
    public GetSosClustersQueryValidator()
    {
    }
}
