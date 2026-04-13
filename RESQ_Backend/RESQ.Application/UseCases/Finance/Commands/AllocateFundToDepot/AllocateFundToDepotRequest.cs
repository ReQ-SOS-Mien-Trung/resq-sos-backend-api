using System.ComponentModel.DataAnnotations;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

public class AllocateFundToDepotRequest
{
    /// <summary>Loại nguồn quỹ: Campaign hoặc SystemFund.</summary>
    [Required]
    public FundSourceType SourceType { get; set; }

    /// <summary>ID chiến dịch - bắt buộc khi SourceType = Campaign, null khi SystemFund.</summary>
    public int? FundCampaignId { get; set; }

    [Required]
    public int DepotId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public string? Purpose { get; set; }
}
