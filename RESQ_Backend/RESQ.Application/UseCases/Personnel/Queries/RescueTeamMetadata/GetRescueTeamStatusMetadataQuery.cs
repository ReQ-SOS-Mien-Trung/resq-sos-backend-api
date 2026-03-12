using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;

public record GetRescueTeamStatusMetadataQuery : IRequest<List<MetadataDto>>;

public class GetRescueTeamStatusMetadataQueryHandler : IRequestHandler<GetRescueTeamStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(GetRescueTeamStatusMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = RescueTeamStatus.AwaitingAcceptance.ToString(), Value = "Chờ xác nhận" },
            new() { Key = RescueTeamStatus.Ready.ToString(), Value = "Sẵn sàng tập hợp" },
            new() { Key = RescueTeamStatus.Gathering.ToString(), Value = "Đang tập hợp" },
            new() { Key = RescueTeamStatus.Available.ToString(), Value = "Sẵn sàng nhiệm vụ" },
            new() { Key = RescueTeamStatus.Assigned.ToString(), Value = "Đã được phân công" },
            new() { Key = RescueTeamStatus.OnMission.ToString(), Value = "Đang làm nhiệm vụ" },
            new() { Key = RescueTeamStatus.Stuck.ToString(), Value = "Gặp sự cố" },
            new() { Key = RescueTeamStatus.Unavailable.ToString(), Value = "Không khả dụng" },
            new() { Key = RescueTeamStatus.Disbanded.ToString(), Value = "Đã giải tán" }
        };

        return await Task.FromResult(result);
    }
}