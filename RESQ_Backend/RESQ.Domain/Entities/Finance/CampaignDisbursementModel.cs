using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Giải ngân từ Campaign → Depot.
/// Ghi nhận mỗi lần Admin cấp tiền (Cách 1) hoặc duyệt FundingRequest (Cách 2).
/// Donor có thể xem được danh sách disbursement + item để biết tiền mua gì.
/// </summary>
public class CampaignDisbursementModel
{
    public int Id { get; private set; }
    public int FundCampaignId { get; private set; }
    public int DepotId { get; private set; }
    public decimal Amount { get; private set; }
    public string? Purpose { get; private set; }
    public DisbursementType Type { get; private set; }
    
    /// <summary>Nếu Type = FundingRequestApproval thì lưu reference tới FundingRequest.</summary>
    public int? FundingRequestId { get; private set; }
    
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    // View properties (populated by mapper/repository)
    public string? FundCampaignName { get; set; }
    public string? DepotName { get; set; }
    public string? CreatedByUserName { get; set; }
    
    private readonly List<DisbursementItemModel> _items = [];
    public IReadOnlyList<DisbursementItemModel> Items => _items.AsReadOnly();

    private CampaignDisbursementModel() { }

    /// <summary>Tạo giải ngân do Admin chủ động cấp (Cách 1).</summary>
    public static CampaignDisbursementModel CreateAdminAllocation(
        int fundCampaignId, int depotId, decimal amount, string? purpose, Guid createdBy)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        return new CampaignDisbursementModel
        {
            FundCampaignId = fundCampaignId,
            DepotId = depotId,
            Amount = amount,
            Purpose = purpose,
            Type = DisbursementType.AdminAllocation,
            FundingRequestId = null,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Tạo giải ngân do duyệt FundingRequest (Cách 2).</summary>
    public static CampaignDisbursementModel CreateFromFundingRequest(
        int fundCampaignId, int depotId, decimal amount, int fundingRequestId, Guid createdBy)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        return new CampaignDisbursementModel
        {
            FundCampaignId = fundCampaignId,
            DepotId = depotId,
            Amount = amount,
            Purpose = $"Duyệt yêu cầu cấp quỹ #{fundingRequestId}",
            Type = DisbursementType.FundingRequestApproval,
            FundingRequestId = fundingRequestId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Reconstitute from DB.</summary>
    public static CampaignDisbursementModel Reconstitute(
        int id, int fundCampaignId, int depotId, decimal amount, string? purpose,
        DisbursementType type, int? fundingRequestId,
        Guid createdBy, DateTime createdAt,
        List<DisbursementItemModel>? items = null)
    {
        var model = new CampaignDisbursementModel
        {
            Id = id,
            FundCampaignId = fundCampaignId,
            DepotId = depotId,
            Amount = amount,
            Purpose = purpose,
            Type = type,
            FundingRequestId = fundingRequestId,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };

        if (items != null)
        {
            model._items.AddRange(items);
        }

        return model;
    }

    public void AddItem(DisbursementItemModel item)
    {
        _items.Add(item);
    }

    public void SetItems(IEnumerable<DisbursementItemModel> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }
}
