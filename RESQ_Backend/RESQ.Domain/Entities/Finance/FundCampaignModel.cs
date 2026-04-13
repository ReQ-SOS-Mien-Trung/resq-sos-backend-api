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
    /// <summary>Tổng số tiền đã được donate (chỉ tăng, không giảm khi rút).</summary>
    public decimal? TotalAmount { get; private set; }
    /// <summary>Số dư hiện tại = TotalAmount - tổng đã giải ngân.</summary>
    public decimal? CurrentBalance { get; private set; }
    
    public FundCampaignStatus Status { get; private set; }

    /// <summary>Lý do tạm dừng chiến dịch - chỉ có giá trị khi Status = Suspended.</summary>
    public string? SuspendReason { get; private set; }

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
        CurrentBalance = 0;
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
        decimal? targetAmount, decimal? totalAmount, decimal? currentBalance,
        FundCampaignStatus status, 
        string? suspendReason,
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
            CurrentBalance = currentBalance,
            Status = status,
            SuspendReason = suspendReason,
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
    // DOMAIN BUSINESS RULES - STATE TRANSITIONS
    // =================================================================

    /// <summary>
    /// Draft → Active.
    /// Điều kiện: Duration đã được thiết lập và EndDate chưa qua.
    /// </summary>
    public void Activate(Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Draft)
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), FundCampaignStatus.Active.ToString());

        if (Duration == null)
            throw new InvalidCampaignDateException("Chiến dịch chưa có thời gian hoạt động. Vui lòng thiết lập ngày bắt đầu và kết thúc trước khi kích hoạt.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (Duration.EndDate < today)
            throw new InvalidCampaignDateException("Không thể kích hoạt chiến dịch đã hết hạn. Vui lòng gia hạn thời gian trước khi kích hoạt.");

        Status = FundCampaignStatus.Active;
        UpdateAudit(modifierId);
    }

    /// <summary>
    /// Active → Suspended.
    /// Điều kiện: Chiến dịch đang Active; phải cung cấp lý do tạm dừng.
    /// </summary>
    public void Suspend(string reason, Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Active)
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), FundCampaignStatus.Suspended.ToString());

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidCampaignDataException("Lý do tạm dừng");

        SuspendReason = reason;
        Status = FundCampaignStatus.Suspended;
        UpdateAudit(modifierId);
    }

    /// <summary>
    /// Suspended → Active.
    /// Điều kiện: Chiến dịch đang Suspended và EndDate chưa qua.
    /// </summary>
    public void Resume(Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Suspended)
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), FundCampaignStatus.Active.ToString());

        if (Duration != null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (Duration.EndDate < today)
                throw new InvalidCampaignDateException("Không thể tiếp tục chiến dịch đã hết hạn. Vui lòng gia hạn thời gian trước khi khôi phục.");
        }

        SuspendReason = null;
        Status = FundCampaignStatus.Active;
        UpdateAudit(modifierId);
    }

    /// <summary>
    /// Active → Closed: Admin đóng thủ công bất kỳ lúc nào, hoặc hệ thống tự đóng khi đến deadline.
    /// Suspended → Closed: luôn được phép.
    /// </summary>
    public void Close(Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Active && Status != FundCampaignStatus.Suspended)
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), FundCampaignStatus.Closed.ToString());

        Status = FundCampaignStatus.Closed;
        UpdateAudit(modifierId);
    }

    /// <summary>
    /// Closed → Archived.
    /// </summary>
    public void Archive(Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Closed)
            throw new InvalidCampaignStatusTransitionException(Status.ToString(), FundCampaignStatus.Archived.ToString());

        Status = FundCampaignStatus.Archived;
        UpdateAudit(modifierId);
    }

    public void UpdateInfo(string name, string region, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == FundCampaignStatus.Closed)
            throw new CampaignClosedOrArchivedException(Status.ToString(), "cập nhật thông tin");

        if (string.IsNullOrWhiteSpace(name)) throw new InvalidCampaignDataException("Tên chiến dịch");
        if (string.IsNullOrWhiteSpace(region)) throw new InvalidCampaignDataException("Khu vực");

        Name = name;
        Region = region;
        UpdateAudit(modifierId);
    }

    public void ExtendDuration(DateOnly newEndDate, Guid modifierId)
    {
        CheckModificationRules();

        if (Status == FundCampaignStatus.Closed)
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

        if (Status != FundCampaignStatus.Draft)
        {
            throw new InvalidCampaignStatusException(
                $"Chỉ được phép điều chỉnh mục tiêu khi chiến dịch ở trạng thái Draft. Trạng thái hiện tại: {Status}.");
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
        CheckModificationRules();

        if (Status != FundCampaignStatus.Active)
        {
            throw new InvalidCampaignStatusException(
                $"Chỉ chiến dịch đang hoạt động (Active) mới được nhận quyên góp. Trạng thái hiện tại: {Status}.");
        }

        if (amount <= 0)
        {
            throw new NegativeMoneyException(amount);
        }

        TotalAmount = (TotalAmount ?? 0) + amount;
        CurrentBalance = (CurrentBalance ?? 0) + amount;
        LastModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Ghi nhận một khoản giải ngân từ chiến dịch: trừ CurrentBalance theo số tiền đã cấp cho kho.
    /// Chỉ được phép khi chiến dịch ở trạng thái Active hoặc Closed.
    /// </summary>
    public void Disburse(decimal amount, Guid modifierId)
    {
        CheckModificationRules();

        if (Status != FundCampaignStatus.Active && Status != FundCampaignStatus.Closed)
        {
            throw new InvalidCampaignStatusException(
                $"Chỉ chiến dịch đang hoạt động (Active) hoặc đã kết thúc (Closed) mới có thể điều phối quỹ cho kho. Trạng thái hiện tại: {Status}.");
        }

        if (amount <= 0)
        {
            throw new NegativeMoneyException(amount);
        }

        var current = CurrentBalance ?? 0;
        if (amount > current)
        {
            throw new InsufficientCampaignFundsException(current, amount);
        }

        CurrentBalance = current - amount;
        UpdateAudit(modifierId);
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
            throw new CampaignDeletedException();

        if (Status == FundCampaignStatus.Archived)
            throw new CampaignArchivedException();
    }

    private void UpdateAudit(Guid modifierId)
    {
        LastModifiedAt = DateTime.UtcNow;
        LastModifiedBy = modifierId;
    }
}
