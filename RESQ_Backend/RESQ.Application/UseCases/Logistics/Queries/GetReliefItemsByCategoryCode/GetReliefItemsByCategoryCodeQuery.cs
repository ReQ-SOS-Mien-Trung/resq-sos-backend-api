using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetReliefItemsByCategoryCode;

/// <summary>
/// Query to get all relief items filtered by category code as metadata (key-value pairs).
/// </summary>
public record GetReliefItemsByCategoryCodeQuery(ItemCategoryCode CategoryCode) : IRequest<List<MetadataDto>>;
