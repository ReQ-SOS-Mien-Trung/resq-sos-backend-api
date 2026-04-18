using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.AbilitySubgroupMetadata;

public record GetAbilitySubgroupMetadataQuery : IRequest<List<MetadataDto>>;
