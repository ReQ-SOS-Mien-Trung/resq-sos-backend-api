using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

public record GetAssemblyPointByIdQuery(int Id) : IRequest<AssemblyPointDto>;
