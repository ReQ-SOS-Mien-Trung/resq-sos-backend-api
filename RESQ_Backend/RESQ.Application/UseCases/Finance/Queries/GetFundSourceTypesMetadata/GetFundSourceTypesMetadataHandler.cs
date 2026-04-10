using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundSourceTypesMetadata;

public class GetFundSourceTypesMetadataHandler : IRequestHandler<GetFundSourceTypesMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetFundSourceTypesMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<FundSourceType>()
            .Select(sourceType => new MetadataDto
            {
                Key = sourceType.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.FundSourceTypeLabels, sourceType.ToString())
            })
            .ToList();

        return Task.FromResult(result);
    }
}
