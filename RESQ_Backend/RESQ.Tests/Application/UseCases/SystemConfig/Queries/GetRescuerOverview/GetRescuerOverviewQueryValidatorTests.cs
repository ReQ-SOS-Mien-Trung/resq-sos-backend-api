using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

public class GetRescuerOverviewQueryValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Validate_InvalidMonths_ReturnsError(int months)
    {
        var validator = new GetRescuerOverviewQueryValidator();

        var result = validator.Validate(new GetRescuerOverviewQuery(months));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRescuerOverviewQuery.Months));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public void Validate_AllowedMonths_ReturnsValid(int months)
    {
        var validator = new GetRescuerOverviewQueryValidator();

        var result = validator.Validate(new GetRescuerOverviewQuery(months));

        Assert.True(result.IsValid);
    }
}
