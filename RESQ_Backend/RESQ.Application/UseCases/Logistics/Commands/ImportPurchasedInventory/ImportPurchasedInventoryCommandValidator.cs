using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandValidator : AbstractValidator<ImportPurchasedInventoryCommand>
{
    public ImportPurchasedInventoryCommandValidator()
    {
        RuleFor(x => x.Invoices)
            .NotEmpty().WithMessage("Danh sách hóa don nh?p hàng không du?c d? tr?ng.");

        RuleForEach(x => x.Invoices).SetValidator(new ImportPurchaseGroupDtoValidator());
    }
}
