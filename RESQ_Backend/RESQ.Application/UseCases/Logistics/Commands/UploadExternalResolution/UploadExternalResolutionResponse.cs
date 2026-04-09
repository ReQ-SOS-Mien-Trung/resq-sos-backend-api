namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

public class UploadExternalResolutionResponse
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    /// <summary>ID bản ghi đóng kho đã được tạo.</summary>
    public int ClosureId { get; set; }

    /// <summary>Số dòng hàng đã ghi nhận thành công.</summary>
    public int ProcessedItemCount { get; set; }

    /// <summary>Tổng tiền thanh lý (Sold) đã chuyển vào quỹ hệ thống. 0 nếu không có item nào được thanh lý.</summary>
    public decimal SoldRevenue { get; set; }

    public string Message { get; set; } = string.Empty;
}
