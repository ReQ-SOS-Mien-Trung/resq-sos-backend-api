using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchaseGroupDtoValidator : AbstractValidator<ImportPurchaseGroupDto>
{
    public ImportPurchaseGroupDtoValidator()
    {
        RuleFor(x => x.VatInvoice)
            .NotNull().WithMessage("Thông tin hóa đơn VAT không được để trống.");

        RuleFor(x => x.VatInvoice.SupplierName)
            .NotEmpty().WithMessage("Tên nhà cung cấp không được để trống.")
            .MaximumLength(255).WithMessage("Tên nhà cung cấp không được vượt quá 255 ký tự.");

        RuleFor(x => x.VatInvoice.InvoiceSerial)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.VatInvoice.InvoiceSerial))
            .WithMessage("Ký hiệu hóa đơn không được vượt quá 50 ký tự.");

        RuleFor(x => x.VatInvoice.InvoiceNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.VatInvoice.InvoiceNumber))
            .WithMessage("Số hóa đơn không được vượt quá 50 ký tự.");

        RuleFor(x => x.VatInvoice.SupplierTaxCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.VatInvoice.SupplierTaxCode))
            .WithMessage("Mã số thuế nhà cung cấp không được vượt quá 50 ký tự.");

        RuleFor(x => x.VatInvoice.InvoiceDate)
            .Must(date => date!.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            .When(x => x.VatInvoice.InvoiceDate.HasValue)
            .WithMessage("Ngày hóa đơn không được là ngày trong tương lai.");

        RuleFor(x => x.VatInvoice.TotalAmount)
            .GreaterThan(0)
            .When(x => x.VatInvoice.TotalAmount.HasValue)
            .WithMessage("Tổng tiền hóa đơn phải lớn hơn 0.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách vật phẩm trong nhóm không được để trống.");

        RuleForEach(x => x.Items).SetValidator(new ImportPurchasedItemDtoValidator());

        RuleFor(x => x.CampaignDisbursementId)
            .GreaterThan(0)
            .When(x => x.CampaignDisbursementId.HasValue)
            .WithMessage("CampaignDisbursementId phải là số nguyên dương.");
    }
}
