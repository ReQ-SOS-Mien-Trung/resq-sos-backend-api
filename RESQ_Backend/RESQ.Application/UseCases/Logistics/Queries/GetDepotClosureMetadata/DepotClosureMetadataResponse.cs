using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;

public class DepotClosureMetadataResponse
{
    /// <summary>Cách xử lý khi đóng kho (dùng cho field resolutionType khi gọi /resolve).</summary>
    public List<MetadataDto> ResolutionTypes { get; set; } = [];

    /// <summary>
    /// Hình thức xử lý tồn kho bên ngoài.
    /// Key = giá trị tiếng Anh (dùng trong API / Excel), Value = tên hiển thị tiếng Việt.
    /// Frontend hiển thị dropdown; nếu có hình thức khác, cho phép nhập tay.
    /// </summary>
    public List<MetadataDto> HandlingMethods { get; set; } = [];
}
