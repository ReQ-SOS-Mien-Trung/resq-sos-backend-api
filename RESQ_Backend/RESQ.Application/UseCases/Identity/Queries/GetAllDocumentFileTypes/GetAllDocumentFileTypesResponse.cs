namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypes;

public class GetAllDocumentFileTypesResponse
{
    public List<DocumentFileTypeDto> Items { get; set; } = new();
}

public class DocumentFileTypeCategoryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DocumentFileTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int? DocumentFileTypeCategoryId { get; set; }
    public DocumentFileTypeCategoryDto? DocumentFileTypeCategory { get; set; }
}
