using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandValidator : AbstractValidator<ImportPurchasedInventoryCommand>
{
    public ImportPurchasedInventoryCommandValidator()
    {
        RuleFor(x => x.AdvancedByName)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.AdvancedByName))
            .WithMessage("Ten nguoi ung khong duoc vuot qua 255 ky tu.");

        RuleFor(x => x.Invoices)
            .NotEmpty().WithMessage("Danh sach hoa don nhap hang khong duoc de trong.");

        RuleForEach(x => x.Invoices).SetValidator(new ImportPurchaseGroupDtoValidator());
    }
}
