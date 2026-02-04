using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPoint;

public record DeleteAssemblyPointCommand(int Id) : IRequest;