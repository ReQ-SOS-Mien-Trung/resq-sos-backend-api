using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

/// <summary>
/// Thrown when attempting to update basic info of an Archived campaign.
/// </summary>
public class CampaignArchivedException : DomainException
{
    public CampaignArchivedException() 
        : base("Không thể cập nhật thông tin khi chiến dịch đã được Lưu trữ (Archived).") { }
}