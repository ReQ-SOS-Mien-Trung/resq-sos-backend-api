using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Giao dịch trên quỹ hệ thống - ghi lại mỗi lần tiền vào/ra quỹ hệ thống.
/// </summary>
public class SystemFundTransactionModel
{
    public int Id { get; set; }
    public int SystemFundId { get; set; }
    public SystemFundTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Loại tham chiếu: "DepotClosure", "DepotFundAllocation", v.v.</summary>
    public string? ReferenceType { get; set; }

    /// <summary>ID của đối tượng tham chiếu.</summary>
    public int? ReferenceId { get; set; }

    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
