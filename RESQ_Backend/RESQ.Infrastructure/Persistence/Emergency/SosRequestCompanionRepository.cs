using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestCompanionRepository(IUnitOfWork unitOfWork) : ISosRequestCompanionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> companions, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<SosRequestCompanion>();
        foreach (var c in companions)
        {
            await repo.AddAsync(new SosRequestCompanion
            {
                SosRequestId = c.SosRequestId,
                UserId = c.UserId,
                PhoneNumber = c.PhoneNumber,
                AddedAt = c.AddedAt
            });
        }
    }

    public async Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequestCompanion>()
            .GetAllByPropertyAsync(x => x.SosRequestId == sosRequestId, includeProperties: "User");

        return entities.Select(e => new SosRequestCompanionRecord(
            e.Id, e.SosRequestId, e.UserId, e.PhoneNumber, e.AddedAt
        )).ToList();
    }

    public async Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequestCompanion>()
            .GetAllByPropertyAsync(x => x.UserId == userId);

        return entities.Select(e => e.SosRequestId).ToList();
    }

    public async Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosRequestCompanion>()
            .GetByPropertyAsync(x => x.SosRequestId == sosRequestId && x.UserId == userId);

        return entity != null;
    }
}
