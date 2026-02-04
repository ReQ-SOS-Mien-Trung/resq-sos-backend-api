using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;

public record GetAssemblyPointStatusMetadataQuery
    : IRequest<List<AssemblyPointStatusMetadataDto>>;
