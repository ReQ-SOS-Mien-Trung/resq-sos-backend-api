using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosClustersQueryValidatorTests
{
    private readonly GetSosClustersQueryValidator _validator = new();

    [Fact]
    public void Validate_Passes_WhenStatusesAreValid()
    {
        var result = _validator.Validate(new GetSosClustersQuery(Statuses: [SosClusterStatus.Pending, SosClusterStatus.Suggested, SosClusterStatus.InProgress, SosClusterStatus.Completed]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenStatusesAreEmpty()
    {
        var result = _validator.Validate(new GetSosClustersQuery());

        Assert.True(result.IsValid);
    }
}
