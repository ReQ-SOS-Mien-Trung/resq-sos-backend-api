using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetSupplyRequestPriorityLevelsQueryHandler
    : IRequestHandler<GetSupplyRequestPriorityLevelsQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetSupplyRequestPriorityLevelsQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<SupplyRequestPriorityLevel>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    SupplyRequestPriorityLevel.Urgent => "Khẩn cấp",
                    SupplyRequestPriorityLevel.High => "Gấp",
                    SupplyRequestPriorityLevel.Medium => "Trung bình",
                    _ => e.ToString()
                }
            })
            .ToList();

        return Task.FromResult(result);
    }
}
