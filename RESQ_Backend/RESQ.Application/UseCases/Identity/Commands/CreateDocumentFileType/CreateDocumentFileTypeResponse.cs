namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileType;

public class CreateDocumentFileTypeResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string Message { get; set; } = null!;
}
