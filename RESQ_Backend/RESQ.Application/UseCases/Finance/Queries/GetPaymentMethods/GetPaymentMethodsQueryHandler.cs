using MediatR;
using RESQ.Application.Common.Models.Finance;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetPaymentMethods;

public class GetPaymentMethodsQueryHandler : IRequestHandler<GetPaymentMethodsQuery, List<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repository;

    public GetPaymentMethodsQueryHandler(IPaymentMethodRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PaymentMethodDto>> Handle(GetPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var methods = await _repository.GetAllActiveAsync(cancellationToken);
        return methods.Select(x => new PaymentMethodDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name
        }).ToList();
    }
}