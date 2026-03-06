namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypeCategories;

public class DocumentFileTypeCategoryItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class GetAllDocumentFileTypeCategoriesResponse
{
    public List<DocumentFileTypeCategoryItemDto> Items { get; set; } = [];
}
