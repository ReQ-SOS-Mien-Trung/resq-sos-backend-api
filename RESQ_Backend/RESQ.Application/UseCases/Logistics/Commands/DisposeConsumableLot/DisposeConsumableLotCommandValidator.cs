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
            .GreaterThan(0).WithMessage("Số lượng xử lý phải lớn hơn 0.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do xử lý không được để trống.")
            .Must(r => AllowedReasons.Contains(r))
            .WithMessage("Lý do xử lý chỉ cho phép: Expired hoặc Damaged.");

        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note != null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
