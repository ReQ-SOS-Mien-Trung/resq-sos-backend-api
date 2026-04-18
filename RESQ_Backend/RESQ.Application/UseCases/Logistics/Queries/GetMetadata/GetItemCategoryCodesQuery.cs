using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public record GetItemCategoryCodesQuery : IRequest<List<MetadataDto>>;
