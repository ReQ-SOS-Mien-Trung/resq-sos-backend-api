namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFund;

/// <summary>
/// DTO quỹ hệ thống - trả về cho Admin.
/// </summary>
public class SystemFundDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
