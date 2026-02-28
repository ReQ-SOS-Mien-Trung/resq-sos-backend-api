namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileType;

public class CreateDocumentFileTypeRequestDto
{
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
