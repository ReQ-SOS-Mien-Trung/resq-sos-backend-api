using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosRequestsByBoundsQueryValidatorTests
{
    private readonly GetSosRequestsByBoundsQueryValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenBoundsAreMissing()
    {
        var result = _validator.Validate(new GetSosRequestsByBoundsQuery());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetSosRequestsByBoundsQuery.MinLat));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetSosRequestsByBoundsQuery.MaxLat));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetSosRequestsByBoundsQuery.MinLng));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetSosRequestsByBoundsQuery.MaxLng));
    }

    [Fact]
    public void Validate_Fails_WhenLatitudeIsOutOfRange()
    {
        var result = _validator.Validate(new GetSosRequestsByBoundsQuery
        {
            MinLat = -91,
            MaxLat = 10,
            MinLng = 106,
            MaxLng = 107
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("minLat"));
    }

    [Fact]
    public void Validate_Fails_WhenBoundsAreInverted()
    {
        var result = _validator.Validate(new GetSosRequestsByBoundsQuery
        {
            MinLat = 11,
            MaxLat = 10,
            MinLng = 107,
            MaxLng = 106
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("minLat"));
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("minLng"));
    }

    [Fact]
    public void Validate_Fails_WhenStatusIsUnknown()
    {
        var result = _validator.Validate(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10,
            MaxLat = 11,
            MinLng = 106,
            MaxLng = 107,
            Statuses = ["UnknownStatus"]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName.StartsWith(nameof(GetSosRequestsByBoundsQuery.Statuses)));
    }

    [Fact]
    public void Validate_Passes_WhenQueryIsValid()
    {
        var result = _validator.Validate(new GetSosRequestsByBoundsQuery
        {
            MinLat = 10,
            MaxLat = 11,
            MinLng = 106,
            MaxLng = 107,
            Statuses = ["Pending", "Assigned"]
        });

        Assert.True(result.IsValid);
    }
}
