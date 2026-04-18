using MediatR;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;

public record ChangeAssemblyPointStatusCommand(
    int Id,
    AssemblyPointStatus Status,
    Guid ChangedBy
) : IRequest<ChangeAssemblyPointStatusResponse>;
