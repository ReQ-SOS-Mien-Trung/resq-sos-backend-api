using System;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Finance.ValueObjects;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

public class FundCampaignModel
{
    // Properties are read-only to the outside world to enforce Encapsulation
    public int Id { get; private set; }
    public string? Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    
    public CampaignDuration? Duration { get; private set; }

    public decimal? TargetAmount { get; private set; }
    public decimal? TotalAmount { get; private set; }
    
    public FundCampaignStatus Status { get; private set; }
    
    public Guid? CreatedBy { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public Guid? LastModifiedBy { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    // Constructor for creating NEW campaigns
    public FundCampaignModel(string name, string region, decimal targetAmount, DateOnly startDate, DateOnly endDate, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidCampaignDataException("Tên chiến dịch");
        if (string.IsNullOrWhiteSpace(region)) throw new InvalidCampaignDataException("Khu vực");
        if (targetAmount <= 0) throw new InvalidCampaignTargetAmountException("Mục tiêu phải lớn hơn 0.");

        Name = name;
        Region = region;
        TargetAmount = targetAmount;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        Status = FundCampaignStatus.Draft;
        TotalAmount = 0;
        IsDeleted = false;

        // Domain Rule: CampaignEndDate must be greater than CampaignStartDate handled in Value Object
        Duration = new CampaignDuration(startDate, endDate);
        
        // Generate a code (Simple logic, can be replaced by domain service)
        Code = $"CP-{DateTime.UtcNow.Ticks}";
    }

    // Private constructor for Hydration (Mapping from Infrastructure)
    private FundCampaignModel() { }

    // Factory method for Mapper to reconstitute state from DB
    public static FundCampaignModel Reconstitute(
        int id, string? code, string name, string region, 
        DateOnly? startDate, DateOnly? endDate, 
        decimal? targetAmount, decimal? totalAmount, 
        FundCampaignStatus status, 
        Guid? createdBy, DateTime? createdAt, 
        Guid? lastModifiedBy, DateTime? lastModifiedAt, 
        bool isDeleted)
    {
        var model = new FundCampaignModel
        {
            Id = id,
            Code = code,
            Name = name,
            Region = region,
            TargetAmount = targetAmount,
            TotalAmount = totalAmount,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            LastModifiedBy = lastModifiedBy,
            LastModifiedAt = lastModifiedAt,
            IsDeleted = isDeleted
        };
        
        if (startDate.HasValue && endDate.HasValue)
        {
            model.Duration = new CampaignDuration(startDate.Value, endDate.Value);
        }

        return model;
    }

    // =================================================================
    // DOMAIN BUSINESS RULES
    // =================================================================

    public void ChangeStatus(FundCampaignStatus newStatus, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == newStatus) return;

        bool isValid = (Status, newStatus) switch
        {
            (FundCampaignStatus.Draft, FundCampaignStatus.Active) => true,
            (FundCampaignStatus.Active, FundCampaignStatus.Suspended) => true,
            (FundCampaignStatus.Suspended, FundCampaignStatus.Active) => true,
            (FundCampaignStatus.Active, FundCampaignStatus.Closed) => true,
            (FundCampaignStatus.Closed, FundCampaignStatus.Archived) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), newStatus.ToString());
        }

        Status = newStatus;
        UpdateAudit(modifierId);
    }

    public void UpdateInfo(string name, string region, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == FundCampaignStatus.Archived)
        {
            throw new CampaignArchivedException();
        }

        if (string.IsNullOrWhiteSpace(name)) throw new InvalidCampaignDataException("Tên chiến dịch");
        if (string.IsNullOrWhiteSpace(region)) throw new InvalidCampaignDataException("Khu vực");

        Name = name;
        Region = region;
        UpdateAudit(modifierId);
    }

    public void ExtendDuration(DateOnly newEndDate, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == FundCampaignStatus.Closed || Status == FundCampaignStatus.Archived)
        {
            throw new CampaignClosedOrArchivedException(Status.ToString(), "gia hạn thời gian");
        }

        if (Duration == null)
        {
             throw new InvalidCampaignDateException("Dữ liệu thời gian chiến dịch chưa được thiết lập.");
        }

        Duration = Duration.Extend(newEndDate);
        UpdateAudit(modifierId);
    }

    public void IncreaseTargetAmount(decimal newTarget, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == FundCampaignStatus.Closed || Status == FundCampaignStatus.Archived)
        {
            throw new CampaignClosedOrArchivedException(Status.ToString(), "thay đổi mục tiêu");
        }

        if (TargetAmount.HasValue && newTarget <= TargetAmount.Value)
        {
            throw new InvalidCampaignTargetAmountException("Mục tiêu mới phải lớn hơn mục tiêu hiện tại.");
        }

        if (TotalAmount.HasValue && TotalAmount.Value > 0 && newTarget < TargetAmount)
        {
             throw new InvalidCampaignTargetAmountException("Đã có khoản quyên góp, không thể giảm mục tiêu chiến dịch.");
        }

        TargetAmount = newTarget;
        UpdateAudit(modifierId);
    }

    public void ReceiveDonation(decimal amount)
    {
        if (IsDeleted)
        {
            throw new CampaignDeletedException();
        }

        if (Status == FundCampaignStatus.Closed || Status == FundCampaignStatus.Archived)
        {
            throw new CampaignClosedOrArchivedException(Status.ToString(), "nhận quyên góp");
        }

        if (amount <= 0)
        {
            throw new NegativeMoneyException(amount);
        }

        TotalAmount = (TotalAmount ?? 0) + amount;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Delete(Guid modifierId)
    {
        // Rule: Already deleted
        if (IsDeleted)
        {
             return; 
        }

        // Rule: Cannot delete if has donations
        if (TotalAmount.HasValue && TotalAmount.Value > 0)
        {
            throw new InvalidCampaignDeleteException("Chiến dịch đã có quyên góp.");
        }

        // Rule: Cannot delete if Archived
        if (Status == FundCampaignStatus.Archived)
        {
            throw new InvalidCampaignDeleteException("Chiến dịch đã được lưu trữ.");
        }

        IsDeleted = true;
        UpdateAudit(modifierId);
    }

    private void CheckModificationRules()
    {
        if (IsDeleted)
        {
            throw new CampaignDeletedException();
        }
    }

    private void UpdateAudit(Guid modifierId)
    {
        LastModifiedAt = DateTime.UtcNow;
        LastModifiedBy = modifierId;
    }
}
