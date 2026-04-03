using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public class ChangeDepotStatusCommandValidator : AbstractValidator<ChangeDepotStatusCommand>
{
    public ChangeDepotStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Trạng thái kho không hợp lệ.")
            .Must(s => s != DepotStatus.Closing && s != DepotStatus.Closed)
            .WithMessage("Không thể đặt trạng thái Closing hoặc Closed qua endpoint này. " +
                         "Vui lòng dùng POST /logistics/depot/{id}/close/initiate để đóng kho.");
    }
}

