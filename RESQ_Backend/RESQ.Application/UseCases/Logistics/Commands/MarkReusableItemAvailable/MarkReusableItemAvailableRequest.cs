using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemAvailable;

/// <param name="Condition">Tình trạng thiết bị sau bảo trì. Bắt buộc chọn: Good, Fair, hoặc Poor.</param>
public record MarkReusableItemAvailableRequest(ReusableItemCondition Condition, string? Note);
