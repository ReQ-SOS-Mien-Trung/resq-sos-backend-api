using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;

public record GetDepotStatusMetadataQuery
    : IRequest<List<DepotStatusMetadataDto>>;
