using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.StartDepotClosing;

public class StartDepotClosingCommandValidator : AbstractValidator<StartDepotClosingCommand>
{
    public StartDepotClosingCommandValidator()
    {
        RuleFor(x => x.DepotId)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Người thực hiện không hợp lệ.");
    }
}
