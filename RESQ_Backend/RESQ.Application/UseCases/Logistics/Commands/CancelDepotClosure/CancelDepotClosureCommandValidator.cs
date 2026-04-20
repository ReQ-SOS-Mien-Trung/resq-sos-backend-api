using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;

public class CancelDepotClosureCommandValidator : AbstractValidator<CancelDepotClosureCommand>
{
    public CancelDepotClosureCommandValidator()
    {
        RuleFor(x => x.DepotId).GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");
        RuleFor(x => x.ClosureId).GreaterThan(0).WithMessage("Id bản ghi đóng kho không hợp lệ.");
        RuleFor(x => x.CancelledBy).NotEmpty().WithMessage("Thông tin người thực hiện không hợp lệ.");
        RuleFor(x => x.CancellationReason)
            .NotEmpty().WithMessage("Lý do hủy không được để trống.")
            .MaximumLength(500).WithMessage("Lý do hủy tối đa 500 ký tự.");
    }
}
