using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

/// <summary>
/// Thrown when attempting to modify a soft-deleted campaign.
/// </summary>
public class CampaignDeletedException : DomainException
{
    public CampaignDeletedException() 
        : base("Không thể cập nhật hoặc thao tác trên chiến dịch đã bị xóa.") { }
}