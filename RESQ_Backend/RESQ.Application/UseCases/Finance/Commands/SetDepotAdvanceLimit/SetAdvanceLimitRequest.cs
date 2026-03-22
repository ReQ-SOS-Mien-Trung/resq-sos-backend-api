namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

/// <summary>
/// Request body cho endpoint cấu hình hạn mức tự ứng.
/// </summary>
public class SetAdvanceLimitRequest
{
    /// <summary>Hạn mức tối đa kho được phép tự ứng (balance âm). 0 = không cho phép âm.</summary>
    public decimal MaxAdvanceLimit { get; set; }
}
