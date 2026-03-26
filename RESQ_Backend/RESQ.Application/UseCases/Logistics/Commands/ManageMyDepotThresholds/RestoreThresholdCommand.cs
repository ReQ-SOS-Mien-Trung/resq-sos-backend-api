using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class RestoreThresholdCommand : IRequest<StockThresholdCommandResponse>
{
    /// <summary>ID người thực hiện (lấy từ JWT).</summary>
    public Guid UserId { get; set; }

    /// <summary>Role của người thực hiện: 1 = Admin, 4 = Manager.</summary>
    public int RoleId { get; set; }

    /// <summary>ID của cấu hình ngưỡng cần kích hoạt lại.</summary>
    public int ConfigId { get; set; }

    /// <summary>Lý do khôi phục (tuỳ chọn).</summary>
    public string? Reason { get; set; }
}
