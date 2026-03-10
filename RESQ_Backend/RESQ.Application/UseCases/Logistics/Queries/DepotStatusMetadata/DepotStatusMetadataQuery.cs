using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;

public record GetDepotStatusMetadataQuery
    : IRequest<List<MetadataDto>>;
