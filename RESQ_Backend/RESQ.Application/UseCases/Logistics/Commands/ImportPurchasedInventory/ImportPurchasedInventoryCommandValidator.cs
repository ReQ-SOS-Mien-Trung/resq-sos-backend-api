using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandValidator : AbstractValidator<ImportPurchasedInventoryCommand>
{
    public ImportPurchasedInventoryCommandValidator()
    {
        RuleFor(x => x.Invoices)
            .NotEmpty().WithMessage("Danh sách hóa đơn nhập hàng không được để trống.");

        RuleForEach(x => x.Invoices).SetValidator(new ImportPurchaseGroupDtoValidator());
    }
}
