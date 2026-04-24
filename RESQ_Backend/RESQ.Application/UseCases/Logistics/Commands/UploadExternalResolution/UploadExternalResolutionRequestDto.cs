namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

/// <summary>
/// Body JSON cho endpoint xử lý tồn kho bên ngoài.
/// Frontend convert file Excel thành JSON rồi gửi lên.
/// </summary>
public class UploadExternalResolutionRequestDto
{
    /// <summary>Danh sách các dòng hàng tồn kho đã xử lý.</summary>
    public List<ExternalResolutionItemDto> Items { get; set; } = [];
}

/// <summary> Một dòng hàng tồn kho đã xử lý, tương ứng với một row trong Excel template.</summary>
public class ExternalResolutionItemDto
{
    /// <summary>Số thứ tự dòng.</summary>
    public int RowNumber { get; set; }
    public int? ItemModelId { get; set; }
    public int? LotId { get; set; }
    public int? ReusableItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? TargetGroup { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string HandlingMethod { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Note { get; set; }

    /// <summary>URL ảnh bằng chứng xử lý cho dòng này (optional).</summary>
    public string? ImageUrl { get; set; }
}
