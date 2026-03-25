using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GenerateDonationImportTemplate;

/// <summary>
/// Tải file Excel mẫu nhập kho từ thiện.
/// Không cần tham số — file template chứa danh mục & vật phẩm từ DB.
/// </summary>
public class GenerateDonationImportTemplateQuery : IRequest<GenerateDonationImportTemplateResult>;

public class GenerateDonationImportTemplateResult
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
