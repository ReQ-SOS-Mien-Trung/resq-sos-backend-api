using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

public class UpdateServiceZoneCommandValidator : AbstractValidator<UpdateServiceZoneCommand>
{
    public UpdateServiceZoneCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Coordinates)
            .NotNull()
            .Must(c => c.Count >= 3)
            .WithMessage("Vùng phục vụ phải có ít nhất 3 điểm tọa độ để tạo thành polygon.");
        RuleForEach(x => x.Coordinates).ChildRules(c =>
        {
            c.RuleFor(p => p.Latitude).InclusiveBetween(-90, 90).WithMessage("Latitude phải trong khoảng [-90, 90].");
            c.RuleFor(p => p.Longitude).InclusiveBetween(-180, 180).WithMessage("Longitude phải trong khoảng [-180, 180].");
        });
    }
}
