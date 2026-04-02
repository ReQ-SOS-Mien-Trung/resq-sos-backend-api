using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public class ExportInventoryCommandValidator : AbstractValidator<ExportInventoryCommand>
{
    public ExportInventoryCommandValidator()
    {
        RuleFor(x => x.ItemModelId)
            .GreaterThan(0).WithMessage("ItemModelId không hợp lệ.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng xuất phải lớn hơn 0.");

        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note != null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
