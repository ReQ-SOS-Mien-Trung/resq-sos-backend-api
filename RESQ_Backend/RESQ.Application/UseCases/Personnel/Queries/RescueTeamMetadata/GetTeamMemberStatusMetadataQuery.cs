using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;

public record GetTeamMemberStatusMetadataQuery : IRequest<List<MetadataDto>>;

public class GetTeamMemberStatusMetadataQueryHandler : IRequestHandler<GetTeamMemberStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(GetTeamMemberStatusMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = TeamMemberStatus.Pending.ToString(), Value = "Chờ phản hồi" },
            new() { Key = TeamMemberStatus.Accepted.ToString(), Value = "Đã tham gia" },
            new() { Key = TeamMemberStatus.Declined.ToString(), Value = "Đã từ chối" },
            new() { Key = TeamMemberStatus.Removed.ToString(), Value = "Đã rời đội" }
        };

        return await Task.FromResult(result);
    }
}