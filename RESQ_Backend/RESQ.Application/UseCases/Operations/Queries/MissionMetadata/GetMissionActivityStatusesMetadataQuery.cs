using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.MissionMetadata;

public record GetMissionActivityStatusesMetadataQuery : IRequest<List<MetadataDto>>;

public class GetMissionActivityStatusesMetadataQueryHandler : IRequestHandler<GetMissionActivityStatusesMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetMissionActivityStatusesMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<MissionActivityStatus>()
            .Select(status => new MetadataDto
            {
                Key = status.ToString(),
                Value = status switch
                {
                    MissionActivityStatus.Planned => "Đã lên kế hoạch",
                    MissionActivityStatus.OnGoing => "Đang thực hiện",
                    MissionActivityStatus.Succeed => "Thành công",
                    MissionActivityStatus.PendingConfirmation => "Chờ xác nhận",
                    MissionActivityStatus.Failed => "Thất bại",
                    MissionActivityStatus.Cancelled => "Đã hủy",
                    _ => status.ToString()
                }
            })
            .ToList();

        return Task.FromResult(result);
    }
}