using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.DisposeConsumableLot;

public class DisposeConsumableLotCommandValidator : AbstractValidator<DisposeConsumableLotCommand>
{
    public DisposeConsumableLotCommandValidator()
    {
        RuleFor(x => x.LotId)
            .GreaterThan(0).WithMessage("LotId không hợp lệ.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng tiêu hủy phải lớn hơn 0.");

        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Nhóm lý do tiêu hủy không được để trống.")
            .MaximumLength(100).WithMessage("Nhóm lý do không được vượt quá 100 ký tự.");

        RuleFor(x => x.Note)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do chi tiết khi tiêu hủy lô hàng.")
            .MaximumLength(500).WithMessage("Lý do chi tiết không được vượt quá 500 ký tự.");
    }
}
