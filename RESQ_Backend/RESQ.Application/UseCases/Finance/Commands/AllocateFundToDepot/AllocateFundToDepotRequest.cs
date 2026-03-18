using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

public class AllocateFundToDepotRequest
{
    [Required]
    public int FundCampaignId { get; set; }

    [Required]
    public int DepotId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public string? Purpose { get; set; }
}
