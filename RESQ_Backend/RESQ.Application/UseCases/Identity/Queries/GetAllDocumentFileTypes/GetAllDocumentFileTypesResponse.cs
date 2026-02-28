namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypes;

public class GetAllDocumentFileTypesResponse
{
    public List<DocumentFileTypeDto> Items { get; set; } = new();
}

public class DocumentFileTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
