using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.GetRoleMetadata;

public record GetRoleMetadataQuery : IRequest<List<MetadataDto>>;