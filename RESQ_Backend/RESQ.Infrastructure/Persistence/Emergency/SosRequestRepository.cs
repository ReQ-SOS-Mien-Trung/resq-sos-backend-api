using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestRepository(IUnitOfWork unitOfWork) : ISosRequestRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
    {
        var entity = SosRequestMapper.ToEntity(sosRequest);
        await _unitOfWork.GetRepository<SosRequest>().AddAsync(entity);
    }

    public async Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == sosRequest.Id,
            tracked: true
        );

        if (existingEntity != null)
        {
            SosRequestMapper.UpdateEntity(existingEntity, sosRequest);
            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(x => x.UserId == userId);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }

    public async Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(filter: null);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }

    public async Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();

        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(x => x.CreatedAt)
        );

        var domainItems = pagedEntities.Items
            .Select(SosRequestMapper.ToDomain)
            .ToList();

        return new PagedResult<SosRequestModel>(
            domainItems,
            pagedEntities.TotalCount,
            pagedEntities.PageNumber,
            pagedEntities.PageSize
        );
    }

    public async Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : SosRequestMapper.ToDomain(entity);
    }
}