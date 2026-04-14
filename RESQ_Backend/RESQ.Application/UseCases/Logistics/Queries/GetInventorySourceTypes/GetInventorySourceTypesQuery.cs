using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;

public record GetInventorySourceTypesQuery : IRequest<List<MetadataDto>>;
