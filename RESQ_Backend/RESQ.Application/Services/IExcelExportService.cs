using RESQ.Domain.Entities.Logistics.Models;

namespace RESQ.Application.Services;

public interface IExcelExportService
{
    /// <summary>
    /// Tạo file Excel báo cáo biến động kho.
    /// </summary>
    /// <param name="rows">Danh sách dòng biến động đã được phân trang/lọc.</param>
    /// <param name="title">Tiêu đề kỳ báo cáo (VD: "Tháng 03/2026").</param>
    /// <param name="depotName">Tên kho (dùng cho tiêu đề và tên file).</param>
    /// <returns>Mảng byte nội dung file .xlsx.</returns>
    byte[] GenerateInventoryMovementReport(IReadOnlyList<InventoryMovementRow> rows, string title, string depotName);
}
