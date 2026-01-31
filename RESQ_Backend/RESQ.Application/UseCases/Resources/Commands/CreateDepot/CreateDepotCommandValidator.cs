using FluentValidation;

namespace RESQ.Application.UseCases.Resources.Commands.CreateDepot;

public class CreateDepotCommandValidator
    : AbstractValidator<CreateDepotCommand>
{
    public CreateDepotCommandValidator()
    {
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
