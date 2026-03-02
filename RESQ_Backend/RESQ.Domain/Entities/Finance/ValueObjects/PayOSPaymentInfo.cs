using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance.ValueObjects;

public record PayOSPaymentInfo
{
    public string? OrderCode { get; init; }
    public string? TransactionId { get; init; }
    public PayOSStatus Status { get; init; }

    public PayOSPaymentInfo(string? orderCode, string? transactionId, PayOSStatus status)
    {
        OrderCode = orderCode;
        TransactionId = transactionId;
        Status = status;
    }

    public static PayOSPaymentInfo New(string? orderCode)
    {
        return new PayOSPaymentInfo(orderCode, null, PayOSStatus.Pending);
    }
    
    public PayOSPaymentInfo UpdateStatus(PayOSStatus newStatus, string? transactionId = null)
    {
        return this with 
        { 
            Status = newStatus, 
            TransactionId = transactionId ?? this.TransactionId 
        };
    }
}