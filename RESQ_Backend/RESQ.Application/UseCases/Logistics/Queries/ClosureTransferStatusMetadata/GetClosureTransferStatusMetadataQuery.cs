using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.ClosureTransferStatusMetadata;

public record GetClosureTransferStatusMetadataQuery
    : IRequest<List<MetadataDto>>;
