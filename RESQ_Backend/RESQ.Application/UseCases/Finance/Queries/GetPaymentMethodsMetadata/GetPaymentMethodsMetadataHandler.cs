using MediatR;
using Microsoft.Extensions.Caching.Memory;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetPaymentMethodsMetadata;

public class GetPaymentMethodsMetadataHandler : IRequestHandler<GetPaymentMethodsMetadataQuery, List<MetadataDto>>
{
    private const string CacheKey = "metadata:payment-methods";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);

    private readonly IMemoryCache _cache;

    public GetPaymentMethodsMetadataHandler(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<List<MetadataDto>> Handle(GetPaymentMethodsMetadataQuery request, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out List<MetadataDto>? cached) && cached is not null)
            return Task.FromResult(cached);

        var result = Enum.GetValues<PaymentMethodCode>()
            .Where(code => !code.IsHidden())
            .Select(code => new MetadataDto
            {
                Key = code.ToString(),
                Value = code.GetDescription()
            })
            .ToList();

        _cache.Set(CacheKey, result, CacheTtl);

        return Task.FromResult(result);
    }
}
