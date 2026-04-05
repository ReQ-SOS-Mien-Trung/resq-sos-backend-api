using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;

public class ResolveDepotClosureRequestDto
{
    public CloseResolutionType ResolutionType { get; set; }

    // --- Option 1: Chuyển sang kho khác ---
    public int? TargetDepotId { get; set; }

    // --- Option 2: Xử lý bên ngoài — ghi chú mô tả cách xử lý ---
    public string? ExternalNote { get; set; }
}
