using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;

public record GetAssemblyPointNameMetadataQuery : IRequest<List<MetadataDto>>;
