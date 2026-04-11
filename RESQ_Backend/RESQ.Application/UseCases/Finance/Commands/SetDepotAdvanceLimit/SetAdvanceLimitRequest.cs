namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

/// <summary>
/// Request body cho endpoint cấu hình hạn mức tự ứng.
/// </summary>
public class SetAdvanceLimitRequest
{
    /// <summary>Hạn mức tối đa tổng tiền ứng trước cho kho này. 0 = không cho phép ứng.</summary>
    public decimal AdvanceLimit { get; set; }
}
