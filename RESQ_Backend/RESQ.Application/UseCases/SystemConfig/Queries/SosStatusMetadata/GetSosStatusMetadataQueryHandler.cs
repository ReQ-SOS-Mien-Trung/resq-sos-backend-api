using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.SystemConfig.Queries.SosStatusMetadata;

public class GetSosStatusMetadataQueryHandler
    : IRequestHandler<GetSosStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetSosStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = SosRequestStatus.Pending.ToString(), Value = "Đang chờ xử lý" },
            new() { Key = SosRequestStatus.Assigned.ToString(), Value = "Đã phân công" },
            new() { Key = SosRequestStatus.InProgress.ToString(), Value = "Đang xử lý" },
            new() { Key = SosRequestStatus.Resolved.ToString(), Value = "Đã giải quyết" },
            new() { Key = SosRequestStatus.Cancelled.ToString(), Value = "Đã hủy" }
        };

        return await Task.FromResult(result);
    }
}
