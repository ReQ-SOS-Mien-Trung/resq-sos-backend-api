using FluentValidation;
using RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;

namespace RESQ.Tests.Application.UseCases.Logistics.GetMyReturnHistoryActivities;

public class GetMyReturnHistoryActivitiesQueryValidatorTests
{
    private readonly GetMyReturnHistoryActivitiesQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsError_WhenPageNumberIsNotPositive()
    {
        var result = _validator.Validate(new GetMyReturnHistoryActivitiesQuery(Guid.NewGuid())
        {
            PageNumber = 0,
            PageSize = 20
        });

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(GetMyReturnHistoryActivitiesQuery.PageNumber));
        Assert.Equal("Số trang phải lớn hơn 0.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsError_WhenPageSizeIsOutOfRange()
    {
        var result = _validator.Validate(new GetMyReturnHistoryActivitiesQuery(Guid.NewGuid())
        {
            PageNumber = 1,
            PageSize = 99
        });

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(GetMyReturnHistoryActivitiesQuery.PageSize));
        Assert.Equal("Kích thước trang phải từ 1 đến 50.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsError_WhenFromDateIsAfterToDate()
    {
        var result = _validator.Validate(new GetMyReturnHistoryActivitiesQuery(Guid.NewGuid())
        {
            FromDate = new DateOnly(2026, 4, 10),
            ToDate = new DateOnly(2026, 4, 9),
            PageNumber = 1,
            PageSize = 20
        });

        var error = Assert.Single(result.Errors);
        Assert.Equal("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.", error.ErrorMessage);
    }
}
