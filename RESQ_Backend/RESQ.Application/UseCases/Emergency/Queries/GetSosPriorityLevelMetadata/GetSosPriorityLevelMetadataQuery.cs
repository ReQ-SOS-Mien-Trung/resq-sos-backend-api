using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosPriorityLevelMetadata;

public record GetSosPriorityLevelMetadataQuery : IRequest<List<MetadataDto>>;

public class GetSosPriorityLevelMetadataQueryHandler : IRequestHandler<GetSosPriorityLevelMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetSosPriorityLevelMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<SosPriorityLevel>().Select(e => new MetadataDto
        {
            Key = e.ToString(),
            Value = e switch
            {
                SosPriorityLevel.Critical => "Rất Nghiêm trọng",
                SosPriorityLevel.High     => "Nghiêm trọng",
                SosPriorityLevel.Medium   => "Trung bình",
                SosPriorityLevel.Low      => "Thấp"
            }
        }).ToList();

        return Task.FromResult(result);
    }
}
