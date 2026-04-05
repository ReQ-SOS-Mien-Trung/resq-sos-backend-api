using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;

public class DepotClosureMetadataResponse
{
    /// <summary>Cách xử lý khi đóng kho (dùng cho field resolutionType khi gọi /resolve).</summary>
    public List<MetadataDto> ResolutionTypes { get; set; } = [];
}
