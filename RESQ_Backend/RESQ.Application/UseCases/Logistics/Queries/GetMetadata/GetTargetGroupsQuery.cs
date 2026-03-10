using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public record GetTargetGroupsQuery : IRequest<List<MetadataDto>>;
