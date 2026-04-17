namespace RESQ.Application.UseCases.Logistics.Commands.DecommissionReusableItem;

/// <summary>
/// Request body cho API ngừng sử dụng (decommission) thiết bị tái sử dụng bị hư hỏng.
/// </summary>
public record DecommissionReusableItemRequest(string? Note);
