namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileTypeCategory;

public class CreateDocumentFileTypeCategoryResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}
