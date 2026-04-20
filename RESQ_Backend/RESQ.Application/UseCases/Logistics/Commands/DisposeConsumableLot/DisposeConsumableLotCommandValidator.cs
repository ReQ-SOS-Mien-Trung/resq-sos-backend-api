using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.DisposeConsumableLot;

public class DisposeConsumableLotCommandValidator : AbstractValidator<DisposeConsumableLotCommand>
{
    private static readonly HashSet<string> AllowedReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Expired", "Damaged"
    };

    public DisposeConsumableLotCommandValidator()
    {
        RuleFor(x => x.LotId)
            .GreaterThan(0).WithMessage("LotId không hợp lệ.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng tiêu hủy phải lớn hơn 0.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Nhóm lý do tiêu hủy không được để trống.")
            .Must(r => AllowedReasons.Contains(r))
            .WithMessage("Nhóm lý do tiêu hủy chỉ cho phép: Expired hoặc Damaged.");

        RuleFor(x => x.Note)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do chi tiết khi tiêu hủy lô hàng.")
            .MaximumLength(500).WithMessage("Lý do chi tiết không được vượt quá 500 ký tự.");
    }
}
