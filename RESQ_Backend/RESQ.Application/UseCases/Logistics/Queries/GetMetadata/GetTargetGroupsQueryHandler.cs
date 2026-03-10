using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetTargetGroupsQueryHandler : IRequestHandler<GetTargetGroupsQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetTargetGroupsQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<TargetGroup>().Select(e => new MetadataDto
        {
            Key = e.ToString(),
            Value = e switch
            {
                TargetGroup.Children => "Trẻ em",
                TargetGroup.Elderly => "Người già",
                TargetGroup.Pregnant => "Phụ nữ mang thai",
                TargetGroup.General => "Chung",
                TargetGroup.Rescuer => "Lực lượng cứu hộ",
                _ => e.ToString()
            }
        }).ToList();

        return Task.FromResult(result);
    }
}
