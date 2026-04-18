namespace RESQ.Domain.Enum.Logistics;

public enum SupplyRequestRoleFilter
{
    /// <summary>Kho hiện tại là kho gửi yêu cầu tiếp tế (RequestingDepot).</summary>
    Requester = 1,

    /// <summary>Kho hiện tại là kho nguồn cung cấp hàng (SourceDepot).</summary>
    Source = 2
}
