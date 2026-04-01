using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCodeMetadata;

public record GetAssemblyPointCodeMetadataQuery : IRequest<List<MetadataDto>>;
