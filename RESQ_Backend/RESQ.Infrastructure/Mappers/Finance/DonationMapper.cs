using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.ValueObjects;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DonationMapper
{
    public static DonationModel ToModel(Donation entity)
    {
        var statusEnum = PayOSStatus.Pending;
        if (!string.IsNullOrEmpty(entity.PayosStatus))
        {
            Enum.TryParse(entity.PayosStatus, true, out statusEnum);
        }

        // Use defaults if data is missing to ensure Value Object validity
        var donorName = entity.DonorName ?? "Anonymous";
        var donorEmail = entity.DonorEmail ?? "no-email@resq.vn";

        return new DonationModel
        {
            Id = entity.Id,
            FundCampaignId = entity.FundCampaignId,
            
            // Reconstruct Value Objects
            Donor = new DonorInfo(donorName, donorEmail),
            Amount = entity.Amount.HasValue ? new Money(entity.Amount.Value) : null,
            
            // Map Primitive Fields
            PayosOrderId = entity.PayosOrderId,
            PayosTransactionId = entity.PayosTransactionId,
            PayosStatus = statusEnum,
            PaidAt = entity.PaidAt,
            Note = entity.Note,
            PaymentAuditInfo = entity.PaymentAuditInfo, // Map new field
            IsPrivate = entity.IsPrivate, 
            CreatedAt = entity.CreatedAt,
            
            // Map View Properties
            FundCampaignName = entity.FundCampaign?.Name,
            FundCampaignCode = entity.FundCampaign?.Code
        };
    }

    public static Donation ToEntity(DonationModel model)
    {
        return new Donation
        {
            Id = model.Id,
            FundCampaignId = model.FundCampaignId,
            DonorName = model.Donor?.Name,
            DonorEmail = model.Donor?.Email,
            Amount = model.Amount?.Amount,
            PayosOrderId = model.PayosOrderId,
            PayosTransactionId = model.PayosTransactionId,
            PayosStatus = model.PayosStatus.ToString(),
            PaidAt = model.PaidAt,
            Note = model.Note,
            PaymentAuditInfo = model.PaymentAuditInfo, // Map new field
            IsPrivate = model.IsPrivate, 
            CreatedAt = model.CreatedAt
        };
    }

    public static void UpdateEntity(Donation entity, DonationModel model)
    {
        if (model.Amount != null) entity.Amount = model.Amount.Amount;
        if (model.Donor != null) 
        {
            entity.DonorName = model.Donor.Name;
            entity.DonorEmail = model.Donor.Email;
        }
        
        entity.PayosOrderId = model.PayosOrderId;
        entity.PayosTransactionId = model.PayosTransactionId;
        entity.PayosStatus = model.PayosStatus.ToString();
        entity.PaidAt = model.PaidAt;
        
        entity.Note = model.Note;
        entity.PaymentAuditInfo = model.PaymentAuditInfo; // Update new field
        entity.IsPrivate = model.IsPrivate; 
    }
}
