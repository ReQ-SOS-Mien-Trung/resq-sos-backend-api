using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosClustersQueryValidatorTests
{
    private readonly GetSosClustersQueryValidator _validator = new();

    [Fact]
    public void Validate_Passes_WhenStatusesAreValid()
    {
        var result = _validator.Validate(new GetSosClustersQuery(Statuses: ["Pending", "suggested", "InProgress", "completed"]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenStatusIsUnknown()
    {
        var result = _validator.Validate(new GetSosClustersQuery(Statuses: ["Unknown"]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.StartsWith(nameof(GetSosClustersQuery.Statuses)));
    }
}
