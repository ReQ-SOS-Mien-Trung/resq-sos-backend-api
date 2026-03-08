using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentMethodRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PaymentMethodModel>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<PaymentMethod>()
            .GetAllByPropertyAsync(x => x.IsActive);

        return entities.Select(e => new PaymentMethodModel
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
            IsActive = e.IsActive
        }).ToList();
    }

    public async Task<PaymentMethodModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<PaymentMethod>()
            .GetByPropertyAsync(x => x.Id == id);

        return entity == null ? null : new PaymentMethodModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive
        };
    }
}