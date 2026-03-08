using System;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Finance.ValueObjects;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

public class DonationModel
{
    public int Id { get; set; }
    public int? FundCampaignId { get; set; }
    
    // Value Objects
    public DonorInfo? Donor { get; set; } 
    public Money? Amount { get; set; }

    // Remaining primitive properties
    public string? PayosOrderId { get; set; }
    public string? PayosTransactionId { get; set; }
    public PayOSStatus PayosStatus { get; private set; } // Private set to enforce logic
    
    // Entity References
    public int? PaymentMethodId { get; set; }
    public string? PaymentMethodCode { get; set; } // e.g. "PAYOS", "MOMO"
    public string? PaymentMethodName { get; set; } 

    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
    
    // New Domain Property
    public string? PaymentAuditInfo { get; set; }

    public bool IsPrivate { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    // View/Logic properties
    public string? FundCampaignName { get; set; }
    public string? FundCampaignCode { get; set; }

    public DonationModel() { }

    public void SetStatus(PayOSStatus status)
    {
        this.PayosStatus = status;
    }

    public void UpdatePaymentStatus(PayOSStatus newStatus)
    {
        if (PayosStatus == PayOSStatus.Succeed)
        {
            if (newStatus == PayOSStatus.Succeed) return;
            throw new InvalidPaymentStatusException(PayosStatus.ToString(), newStatus.ToString());
        }

        if (PayosStatus == PayOSStatus.Failed)
        {
            if (newStatus == PayOSStatus.Failed) return;
            throw new InvalidPaymentStatusException(PayosStatus.ToString(), newStatus.ToString());
        }

        PayosStatus = newStatus;
    }
}
