using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.SystemConfig.Queries.SosStatusMetadata;

public record GetSosStatusMetadataQuery
    : IRequest<List<MetadataDto>>;
