using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public record GetSourceDepotStatusesQuery : IRequest<List<MetadataDto>>;
