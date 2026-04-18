namespace RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;

public class ChangeAssemblyPointStatusResponse
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
