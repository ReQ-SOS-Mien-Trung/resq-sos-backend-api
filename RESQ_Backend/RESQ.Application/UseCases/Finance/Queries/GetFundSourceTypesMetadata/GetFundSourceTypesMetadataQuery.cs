using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundSourceTypesMetadata;

/// <summary>
/// Trả về danh sách loại nguồn quỹ dạng key-value cho FE.
/// </summary>
public record GetFundSourceTypesMetadataQuery : IRequest<List<MetadataDto>>;
