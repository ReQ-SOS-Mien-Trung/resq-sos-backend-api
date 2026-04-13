using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.ValueObjects;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DonationMapper
{
    public static DonationModel ToModel(Donation entity)
    {
        var statusEnum = Status.Pending;
        if (!string.IsNullOrEmpty(entity.Status))
        {
            Enum.TryParse(entity.Status, true, out statusEnum);
        }

        var donorName = entity.DonorName ?? "Anonymous";
        var donorEmail = entity.DonorEmail ?? "no-email@resq.vn";

        var model = new DonationModel
        {
            Id = entity.Id,
            FundCampaignId = entity.FundCampaignId,
            Donor = new DonorInfo(donorName, donorEmail),
            Amount = entity.Amount.HasValue ? new Money(entity.Amount.Value) : null,
            OrderId = entity.OrderId,
            TransactionId = entity.TransactionId,
            
            // PaymentMethodCode lấy trực tiếp từ enum (đã lưu dưới dạng string)
            PaymentMethodCode = entity.PaymentMethodCode,

            PaidAt = entity.PaidAt,
            Note = entity.Note,
            PaymentAuditInfo = entity.PaymentAuditInfo, 
            IsPrivate = entity.IsPrivate, 
            CreatedAt = entity.CreatedAt,
            FundCampaignName = entity.FundCampaign?.Name,
            FundCampaignCode = entity.FundCampaign?.Code
        };
        
        model.SetStatus(statusEnum);
        return model;
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
            OrderId = model.OrderId,
            TransactionId = model.TransactionId,
            Status = model.Status.ToString(),
            PaymentMethodCode = model.PaymentMethodCode,
            PaidAt = model.PaidAt,
            Note = model.Note,
            PaymentAuditInfo = model.PaymentAuditInfo, 
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
        
        entity.OrderId = model.OrderId;
        entity.TransactionId = model.TransactionId;
        entity.Status = model.Status.ToString();
        entity.PaymentMethodCode = model.PaymentMethodCode;
        entity.PaidAt = model.PaidAt;
        
        entity.Note = model.Note;
        entity.PaymentAuditInfo = model.PaymentAuditInfo; 
        entity.IsPrivate = model.IsPrivate; 
    }
}


