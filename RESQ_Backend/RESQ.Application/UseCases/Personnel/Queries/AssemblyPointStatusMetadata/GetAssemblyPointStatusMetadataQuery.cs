using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;

public record GetAssemblyPointStatusMetadataQuery
    : IRequest<List<MetadataDto>>;
