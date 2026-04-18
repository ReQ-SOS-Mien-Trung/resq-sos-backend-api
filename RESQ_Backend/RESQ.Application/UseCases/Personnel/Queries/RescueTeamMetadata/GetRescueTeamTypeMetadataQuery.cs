using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;

public record GetRescueTeamTypeMetadataQuery : IRequest<List<MetadataDto>>;

public class GetRescueTeamTypeMetadataQueryHandler : IRequestHandler<GetRescueTeamTypeMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(GetRescueTeamTypeMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = RescueTeamType.Rescue.ToString(), Value = "Cứu hộ" },
            new() { Key = RescueTeamType.Medical.ToString(), Value = "Y tế" },
            new() { Key = RescueTeamType.Transportation.ToString(), Value = "Vận chuyển" },
            new() { Key = RescueTeamType.Mixed.ToString(), Value = "Hỗn hợp" }
        };

        return await Task.FromResult(result);
    }
}
