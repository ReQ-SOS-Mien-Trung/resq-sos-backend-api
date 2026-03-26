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

    /// <summary>
    /// Tạo file Excel mẫu để nhập kho từ thiện (donation import).
    /// File có dropdown chọn danh mục → dependent dropdown chọn vật phẩm,
    /// và auto-fill VLOOKUP cho Đối tượng / Loại vật phẩm / Đơn vị.
    /// </summary>
    /// <param name="categories">Danh sách danh mục (Id, Code, Name).</param>
    /// <param name="items">Danh sách vật phẩm kèm target groups.</param>
    /// <param name="targetGroups">Danh sách đối tượng lấy trực tiếp từ bảng target_groups.</param>
    /// <returns>Mảng byte nội dung file .xlsx.</returns>
    byte[] GenerateDonationImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups);

    /// <summary>
    /// Tạo file Excel mẫu để nhập kho mua sắm (purchase import).
    /// Tương tự donation template nhưng có thêm cột thông tin hóa đơn VAT và đơn giá.
    /// </summary>
    /// <param name="categories">Danh sách danh mục (Id, Code, Name).</param>
    /// <param name="items">Danh sách vật phẩm kèm target groups.</param>
    /// <param name="targetGroups">Danh sách đối tượng lấy trực tiếp từ bảng target_groups.</param>
    /// <returns>Mảng byte nội dung file .xlsx.</returns>
    byte[] GeneratePurchaseImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups);
}

/// <summary>Thông tin danh mục cho Excel template nhập kho.</summary>
public record DonationImportCategoryInfo(int Id, string Code, string Name);

/// <summary>Thông tin đối tượng (target group) cho Excel template nhập kho.</summary>
public record DonationImportTargetGroupInfo(
    int Id,
    string Name,
    string NameDisplay);

/// <summary>Thông tin vật phẩm cho Excel template nhập kho.</summary>
public record DonationImportItemInfo(
    int Id,
    string Name,
    string CategoryCode,
    string TargetGroupDisplay,
    string TargetGroupRaw,
    string ItemTypeDisplay,
    string ItemTypeRaw,
    string Unit,
    string Description = "");
