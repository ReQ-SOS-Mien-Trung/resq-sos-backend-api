using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestCommandValidator : AbstractValidator<CreateSosRequestCommand>
{
    // Bounding box xấp xỉ bao phủ các tỉnh Miền Trung Việt Nam
    // (Thanh Hoá → Bình Thuận, bao gồm Tây Nguyên tiếp giáp)
    private const double LatMin = 10.3;
    private const double LatMax = 20.5;
    private const double LonMin = 103.0;
    private const double LonMax = 109.5;

    public CreateSosRequestCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RawMessage).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Location.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Location.Longitude).InclusiveBetween(-180, 180);

        RuleFor(x => x.Location)
            .Must(loc =>
                loc.Latitude  >= LatMin && loc.Latitude  <= LatMax &&
                loc.Longitude >= LonMin && loc.Longitude <= LonMax)
            .WithMessage("Ứng dụng chỉ hỗ trợ khu vực Miền Trung Việt Nam. Vị trí của bạn nằm ngoài vùng phục vụ.");
    }
}