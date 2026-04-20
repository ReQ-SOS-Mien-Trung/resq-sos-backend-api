using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public class ChangeDepotStatusCommandValidator : AbstractValidator<ChangeDepotStatusCommand>
{
    public ChangeDepotStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Người thực hiện không hợp lệ.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Trạng thái kho không hợp lệ.")
            .Must(s => s == DepotStatus.Available || s == DepotStatus.Unavailable)
            .WithMessage("Trạng thái đưa vào không hợp lệ. Các trạng thái được phép: Available, Unavailable.");
    }
}
