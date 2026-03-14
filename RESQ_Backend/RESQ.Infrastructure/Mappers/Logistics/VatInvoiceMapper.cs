using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class VatInvoiceMapper
{
    public static VatInvoice ToEntity(VatInvoiceModel model)
    {
        return new VatInvoice
        {
            InvoiceSerial = model.InvoiceSerial,
            InvoiceNumber = model.InvoiceNumber,
            SupplierName = model.SupplierName,
            SupplierTaxCode = model.SupplierTaxCode,
            InvoiceDate = model.InvoiceDate,
            TotalAmount = model.TotalAmount,
            FileUrl = model.FileUrl,
            CreatedAt = model.CreatedAt ?? DateTime.UtcNow
        };
    }

    public static VatInvoiceModel ToDomain(VatInvoice entity)
    {
        return new VatInvoiceModel
        {
            Id = entity.Id,
            InvoiceSerial = entity.InvoiceSerial,
            InvoiceNumber = entity.InvoiceNumber,
            SupplierName = entity.SupplierName,
            SupplierTaxCode = entity.SupplierTaxCode,
            InvoiceDate = entity.InvoiceDate,
            TotalAmount = entity.TotalAmount,
            FileUrl = entity.FileUrl,
            CreatedAt = entity.CreatedAt
        };
    }
}
