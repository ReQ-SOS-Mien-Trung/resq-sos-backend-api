using MediatR;
using RESQ.Application.Common.Models.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetPaymentMethods;

public record GetPaymentMethodsQuery : IRequest<List<PaymentMethodDto>>;