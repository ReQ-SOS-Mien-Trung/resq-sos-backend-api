using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

public class DepotFundsResponseDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal AdvanceLimit { get; set; }
    public decimal OutstandingAdvanceAmount { get; set; }
    public List<DepotFundItemDto> Funds { get; set; } = new();
}

public class DepotFundItemDto
{
    public int Id { get; set; }
    public decimal Balance { get; set; }
    public FundSourceType? FundSourceType { get; set; }
    public string? FundSourceName { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
