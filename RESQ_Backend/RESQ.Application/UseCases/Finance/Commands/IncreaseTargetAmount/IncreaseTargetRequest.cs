using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.IncreaseTargetAmount;

public class IncreaseTargetRequest
{
    [Required]
    [Range(1000, double.MaxValue, ErrorMessage = "Target must be greater than 1000")]
    public decimal NewTarget { get; set; }
}
