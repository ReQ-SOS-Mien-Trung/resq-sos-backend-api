using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;

public class ChangeAssemblyPointStatusRequestDto
{
    public AssemblyPointStatus Status { get; set; }
}