using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;

public record GetDepotClosureMetadataQuery : IRequest<DepotClosureMetadataResponse>;
