using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GeneratePurchaseImportTemplate;

/// <summary>
/// Tải file Excel mẫu nhập kho mua sắm (purchase import template).
/// Không cần tham số - file template chứa danh mục & vật phẩm từ DB.
/// </summary>
public class GeneratePurchaseImportTemplateQuery : IRequest<GeneratePurchaseImportTemplateResult>;

public class GeneratePurchaseImportTemplateResult
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
