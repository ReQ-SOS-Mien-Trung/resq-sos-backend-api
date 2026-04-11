using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Giao dịch quỹ kho — ghi lại mỗi lần cộng/trừ quỹ để audit trail.
/// </summary>
public class DepotFundTransactionModel
{
    public int Id { get; set; }
    public int DepotFundId { get; set; }
    public DepotFundTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    
    /// <summary>Loại tham chiếu: "CampaignDisbursement", "VatInvoice", v.v.</summary>
    public string? ReferenceType { get; set; }
    
    /// <summary>ID của đối tượng tham chiếu.</summary>
    public int? ReferenceId { get; set; }
    
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Tên người ứng trước / người được hoàn trả.</summary>
    public string? ContributorName { get; set; }


    /// <summary>ID người ứng trước / người được hoàn trả (nullable nếu là người bên ngoài hệ thống).</summary>
    public Guid? ContributorId { get; set; }
}
