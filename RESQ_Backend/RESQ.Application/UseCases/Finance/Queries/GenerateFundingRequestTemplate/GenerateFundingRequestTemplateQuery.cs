using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GenerateFundingRequestTemplate;

/// <summary>
/// Tải file Excel mẫu yêu cầu cấp tiền (funding request template).
/// Không cần tham số - file template chứa danh mục & vật phẩm từ DB.
/// </summary>
public class GenerateFundingRequestTemplateQuery : IRequest<GenerateFundingRequestTemplateResult>;

public class GenerateFundingRequestTemplateResult
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
