using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;

public record GetInventoryActionTypesQuery : IRequest<List<MetadataDto>>;