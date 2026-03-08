using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IPaymentMethodRepository
{
    Task<List<PaymentMethodModel>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<PaymentMethodModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}