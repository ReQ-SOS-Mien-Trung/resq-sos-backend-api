using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;

public class GetSosRequestsByBoundsQueryValidator : AbstractValidator<GetSosRequestsByBoundsQuery>
{
    public GetSosRequestsByBoundsQueryValidator()
    {
        RuleFor(x => x.MinLat)
            .NotNull()
            .WithMessage("minLat is required.");

        RuleFor(x => x.MaxLat)
            .NotNull()
            .WithMessage("maxLat is required.");

        RuleFor(x => x.MinLng)
            .NotNull()
            .WithMessage("minLng is required.");

        RuleFor(x => x.MaxLng)
            .NotNull()
            .WithMessage("maxLng is required.");

        RuleFor(x => x.MinLat!.Value)
            .InclusiveBetween(-90d, 90d)
            .When(x => x.MinLat.HasValue)
            .WithMessage("minLat must be between -90 and 90.");

        RuleFor(x => x.MaxLat!.Value)
            .InclusiveBetween(-90d, 90d)
            .When(x => x.MaxLat.HasValue)
            .WithMessage("maxLat must be between -90 and 90.");

        RuleFor(x => x.MinLng!.Value)
            .InclusiveBetween(-180d, 180d)
            .When(x => x.MinLng.HasValue)
            .WithMessage("minLng must be between -180 and 180.");

        RuleFor(x => x.MaxLng!.Value)
            .InclusiveBetween(-180d, 180d)
            .When(x => x.MaxLng.HasValue)
            .WithMessage("maxLng must be between -180 and 180.");

        RuleFor(x => x)
            .Must(x => !x.MinLat.HasValue || !x.MaxLat.HasValue || x.MinLat.Value <= x.MaxLat.Value)
            .WithMessage("minLat must be less than or equal to maxLat.");

        RuleFor(x => x)
            .Must(x => !x.MinLng.HasValue || !x.MaxLng.HasValue || x.MinLng.Value <= x.MaxLng.Value)
            .WithMessage("minLng must be less than or equal to maxLng.");
    }
}
