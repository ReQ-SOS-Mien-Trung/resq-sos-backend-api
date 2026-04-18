namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchaseGroupResultDto
{
    public int GroupIndex { get; set; }
    public int? VatInvoiceId { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public List<ImportPurchasedErrorDto> Errors { get; set; } = new();

    /// <summary>
    /// Số vật phẩm đã tự động ghi vào bảng công khai disbursement_items
    /// (chỉ có giá trị khi CampaignDisbursementId được cung cấp).
    /// </summary>
    public int? DisbursementItemsLogged { get; set; }
}
