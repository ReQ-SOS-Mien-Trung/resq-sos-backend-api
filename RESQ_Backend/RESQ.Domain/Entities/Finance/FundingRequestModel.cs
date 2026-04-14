using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Yêu c?u c?p thêm qu? t? Depot ? Admin (Cách 2).
/// Depot dính kèm file Excel v?t ph?m + giá ti?n.
/// Admin duy?t ? ch?n campaign ? h? th?ng gi?i ngân.
/// </summary>
public class FundingRequestModel
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Description { get; private set; }
    
    /// <summary>URL file Excel dính kèm (v?t ph?m + giá ti?n).</summary>
    public string? AttachmentUrl { get; private set; }
    
    public FundingRequestStatus Status { get; private set; }
    
    /// <summary>Campaign mà Admin ch?n d? rút ti?n (ch? có khi dã duy?t).</summary>
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

    /// <summary>Depot t?o yêu c?u c?p qu? m?i.</summary>
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

    /// <summary>Admin duy?t yêu c?u - ch?n ngu?n qu? (campaign ho?c system fund).</summary>
    public void Approve(int? campaignId, Guid reviewerId)
    {
        if (Status != FundingRequestStatus.Pending)
            throw new InvalidFundingRequestStatusException(Status.ToString(), "duy?t");

        Status = FundingRequestStatus.Approved;
        ApprovedCampaignId = campaignId;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>Admin t? ch?i yêu c?u.</summary>
    public void Reject(Guid reviewerId, string reason)
    {
        if (Status != FundingRequestStatus.Pending)
            throw new InvalidFundingRequestStatusException(Status.ToString(), "t? ch?i");

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
