using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetTargetGroupsQueryHandler : IRequestHandler<GetTargetGroupsQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetTargetGroupsQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<TargetGroup>().Select(e => new MetadataDto
        {
            Key   = e.ToString(),
            Value = TargetGroupTranslations.ToVietnamese(e.ToString())
        }).ToList();

        return Task.FromResult(result);
    }
}
