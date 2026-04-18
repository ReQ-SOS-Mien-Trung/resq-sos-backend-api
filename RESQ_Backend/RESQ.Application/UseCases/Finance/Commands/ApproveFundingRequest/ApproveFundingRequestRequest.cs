using System.ComponentModel.DataAnnotations;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

public class ApproveFundingRequestRequest
{
    /// <summary>Nguồn quỹ: "Campaign" hoặc "SystemFund".</summary>
    [Required]
    public FundSourceType SourceType { get; set; }

    /// <summary>Campaign mà Admin chọn để rút tiền (bắt buộc khi SourceType = Campaign, bỏ qua khi SystemFund).</summary>
    public int? CampaignId { get; set; }
}
