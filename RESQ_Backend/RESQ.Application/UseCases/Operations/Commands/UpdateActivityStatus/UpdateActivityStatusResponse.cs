using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusResponse
{
    public int ActivityId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid DecisionBy { get; set; }
    public string? ImageUrl { get; set; }
    public List<SupplyExecutionItemDto> ConsumedItems { get; set; } = [];
}
