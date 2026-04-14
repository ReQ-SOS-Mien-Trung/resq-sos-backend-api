using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public class ChangeDepotStatusCommandValidator : AbstractValidator<ChangeDepotStatusCommand>
{
    public ChangeDepotStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id kho ph?i l?n hon 0.");

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Ngu?i th?c hi?n không h?p l?.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Tr?ng thái kho không h?p l?.")
            .Must(s => s == DepotStatus.Available || s == DepotStatus.Unavailable || s == DepotStatus.Closing)
            .WithMessage("Tr?ng thái dua vào không h?p l?. Các tr?ng thái du?c phép: Available, Unavailable, Closing.");
    }
}
