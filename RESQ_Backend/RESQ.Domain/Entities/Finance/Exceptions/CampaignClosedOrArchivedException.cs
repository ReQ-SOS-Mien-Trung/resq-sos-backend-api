using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

/// <summary>
/// Thrown when attempting to extend duration or change targets on Closed/Archived campaigns.
/// </summary>
public class CampaignClosedOrArchivedException : DomainException
{
    public CampaignClosedOrArchivedException(string status, string action) 
        : base($"Không thể {action} khi chiến dịch đang ở trạng thái {status}.") { }
}