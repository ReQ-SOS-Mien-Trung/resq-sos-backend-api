using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

public class AdjustInventoryCommandValidator : AbstractValidator<AdjustInventoryCommand>
{
    public AdjustInventoryCommandValidator()
    {
        RuleFor(x => x.ItemModelId)
            .GreaterThan(0).WithMessage("ItemModelId không hợp lệ.");

        RuleFor(x => x.QuantityChange)
            .NotEqual(0).WithMessage("Số lượng điều chỉnh phải khác 0.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.");

        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note != null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");

        RuleFor(x => x.ExpiredDate)
            .GreaterThan(DateTime.UtcNow).When(x => x.ExpiredDate.HasValue)
            .WithMessage("Ngày hết hạn phải ở tương lai.");
    }
}
