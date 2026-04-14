using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotMetadata;

/// <summary>
/// [Metadata] Danh sách kho dùng cho dropdown  key = id, value = tên.
/// </summary>
public record GetDepotMetadataQuery : IRequest<List<MetadataDto>>
{
    public bool ExcludeUnavailableAndClosed { get; init; } = false;
}
