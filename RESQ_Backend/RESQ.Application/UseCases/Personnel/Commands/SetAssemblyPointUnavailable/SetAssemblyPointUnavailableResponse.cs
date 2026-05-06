namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

using RESQ.Application.Common.Models;

public class SetAssemblyPointUnavailableResponse
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AssemblyPointUnavailableImpactResponse? Impact { get; set; }
}

