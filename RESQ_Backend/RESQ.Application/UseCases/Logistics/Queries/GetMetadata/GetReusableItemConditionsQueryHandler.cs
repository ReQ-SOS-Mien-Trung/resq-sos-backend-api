using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetReusableItemConditionsQueryHandler : IRequestHandler<GetReusableItemConditionsQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetReusableItemConditionsQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ReusableItemCondition>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    ReusableItemCondition.Good => "Tốt",
                    ReusableItemCondition.Fair => "Trung bình",
                    ReusableItemCondition.Poor => "Kém",
                    _                          => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
