using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Yêu cầu cấp thêm quỹ từ Depot → Admin (Cách 2).
/// Depot đính kèm file Excel vật tư + giá tiền.
/// Admin duyệt → chọn campaign → hệ thống giải ngân.
/// </summary>
public class FundingRequestModel
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Description { get; private set; }
    
    /// <summary>URL file Excel đính kèm (vật tư + giá tiền).</summary>
    public string? AttachmentUrl { get; private set; }
    
    public FundingRequestStatus Status { get; private set; }
    
    /// <summary>Campaign mà Admin chọn để rút tiền (chỉ có khi đã duyệt).</summary>
    public int? ApprovedCampaignId { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    
    // View properties
    public string? DepotName { get; set; }
    public string? RequestedByUserName { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? ApprovedCampaignName { get; set; }
    
    private readonly List<FundingRequestItemModel> _items = [];
    public IReadOnlyList<FundingRequestItemModel> Items => _items.AsReadOnly();

    private FundingRequestModel() { }

    /// <summary>Depot tạo yêu cầu cấp quỹ mới.</summary>
    public FundingRequestModel(int depotId, Guid requestedBy, decimal totalAmount, string? description, string? attachmentUrl)
    {
        if (totalAmount <= 0) throw new NegativeMoneyException(totalAmount);

        DepotId = depotId;
        RequestedBy = requestedBy;
        TotalAmount = totalAmount;
        Description = description;
        AttachmentUrl = attachmentUrl;
        Status = FundingRequestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Reconstitute from DB.</summary>
    public static FundingRequestModel Reconstitute(
        int id, int depotId, Guid requestedBy, decimal totalAmount,
        string? description, string? attachmentUrl,
        FundingRequestStatus status,
        int? approvedCampaignId, Guid? reviewedBy, DateTime? reviewedAt,
        string? rejectionReason, DateTime createdAt,
        List<FundingRequestItemModel>? items = null)
    {
        var model = new FundingRequestModel
        {
            Id = id,
            DepotId = depotId,
            RequestedBy = requestedBy,
            TotalAmount = totalAmount,
            Description = description,
            AttachmentUrl = attachmentUrl,
            Status = status,
            ApprovedCampaignId = approvedCampaignId,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            RejectionReason = rejectionReason,
            CreatedAt = createdAt
        };

        if (items != null)
        {
            model._items.AddRange(items);
        }

        return model;
    }

    /// <summary>Admin duyệt yêu cầu — chọn campaign để rút tiền.</summary>
    public void Approve(int campaignId, Guid reviewerId)
    {
        if (Status != FundingRequestStatus.Pending)
            throw new InvalidFundingRequestStatusException(Status.ToString(), "duyệt");

        Status = FundingRequestStatus.Approved;
        ApprovedCampaignId = campaignId;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>Admin từ chối yêu cầu.</summary>
    public void Reject(Guid reviewerId, string reason)
    {
        if (Status != FundingRequestStatus.Pending)
            throw new InvalidFundingRequestStatusException(Status.ToString(), "từ chối");

        Status = FundingRequestStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }

    public void AddItem(FundingRequestItemModel item)
    {
        _items.Add(item);
    }

    public void SetItems(IEnumerable<FundingRequestItemModel> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }
}
