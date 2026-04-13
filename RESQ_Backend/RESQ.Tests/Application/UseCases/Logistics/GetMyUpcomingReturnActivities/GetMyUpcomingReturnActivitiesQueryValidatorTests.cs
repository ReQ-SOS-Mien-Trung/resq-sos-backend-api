using RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Logistics.GetMyUpcomingReturnActivities;

public class GetMyUpcomingReturnActivitiesQueryValidatorTests
{
    private readonly GetMyUpcomingReturnActivitiesQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsError_WhenStatusIsNotAllowed()
    {
        var result = _validator.Validate(new GetMyUpcomingReturnActivitiesQuery(Guid.NewGuid())
        {
            Status = MissionActivityStatus.Planned,
            PageNumber = 1,
            PageSize = 20
        });

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(GetMyUpcomingReturnActivitiesQuery.Status));
        Assert.Equal("Status phải là OnGoing hoặc PendingConfirmation.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsError_WhenPageSizeIsOutOfRange()
    {
        var result = _validator.Validate(new GetMyUpcomingReturnActivitiesQuery(Guid.NewGuid())
        {
            Status = MissionActivityStatus.OnGoing,
            PageNumber = 1,
            PageSize = 0
        });

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(GetMyUpcomingReturnActivitiesQuery.PageSize));
        Assert.Equal("Kích thước trang phải từ 1 đến 50.", error.ErrorMessage);
    }
}
