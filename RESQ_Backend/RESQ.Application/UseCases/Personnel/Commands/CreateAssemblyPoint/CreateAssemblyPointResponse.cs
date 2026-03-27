namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public class CreateAssemblyPointResponse
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int MaxCapacity { get; set; }
    public string Status { get; set; } = string.Empty;
}
