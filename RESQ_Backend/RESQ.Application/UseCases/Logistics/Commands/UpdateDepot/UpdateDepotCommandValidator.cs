using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public class UpdateDepotCommandValidator : AbstractValidator<UpdateDepotCommand>
{
    public UpdateDepotCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => x.Location.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Location.Longitude)
            .InclusiveBetween(-180, 180);

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Sức chứa kho phải lớn hơn 0");
    }
}
