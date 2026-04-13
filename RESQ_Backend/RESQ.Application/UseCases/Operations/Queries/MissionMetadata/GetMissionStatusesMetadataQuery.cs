using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.MissionMetadata;

public record GetMissionStatusesMetadataQuery : IRequest<List<MetadataDto>>;

public class GetMissionStatusesMetadataQueryHandler : IRequestHandler<GetMissionStatusesMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetMissionStatusesMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<MissionStatus>()
            .Select(status => new MetadataDto
            {
                Key = status.ToString(),
                Value = status switch
                {
                    MissionStatus.Planned => "Đã lên kế hoạch",
                    MissionStatus.OnGoing => "Đang diễn ra",
                    MissionStatus.Completed => "Hoàn thành",
                    MissionStatus.Incompleted => "Không hoàn thành",
                    _ => status.ToString()
                }
            })
            .ToList();

        return Task.FromResult(result);
    }
}
