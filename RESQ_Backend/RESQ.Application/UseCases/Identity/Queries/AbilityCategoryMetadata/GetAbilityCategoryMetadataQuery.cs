using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.AbilityCategoryMetadata;

public record GetAbilityCategoryMetadataQuery : IRequest<List<MetadataDto>>;
