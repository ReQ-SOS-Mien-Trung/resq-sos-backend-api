namespace RESQ.Application.UseCases.Logistics.Commands.DisposeConsumableLot;

/// <summary>
/// Request body cho API xử lý (dispose) đồ tiêu hao theo lô.
/// Reason chỉ cho phép: Expired hoặc Damaged.
/// </summary>
public record DisposeConsumableLotRequest(
    int LotId,
    int Quantity,
    string Reason,
    string? Note);
