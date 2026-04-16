using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Infrastructure.Persistence.System;

public class AiConfigRepository(
    IUnitOfWork unitOfWork,
    IAiSecretProtector aiSecretProtector) : IAiConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAiSecretProtector _aiSecretProtector = aiSecretProtector;

    public async Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AiConfig>()
            .AsQueryable()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AiConfig>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(config);
        await _unitOfWork.GetRepository<AiConfig>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        config.Id = entity.Id;
    }

    public async Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(config);
        await _unitOfWork.GetRepository<AiConfig>().UpdateAsync(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<AiConfig>().DeleteAsyncById(id);
    }

    public async Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = name.Trim().ToLower();

        return await _unitOfWork.GetRepository<AiConfig>()
            .AsQueryable()
            .AnyAsync(
                x => x.Name != null
                     && x.Name.ToLower() == normalizedName
                     && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalizedVersion = version.Trim().ToLower();

        return await _unitOfWork.GetRepository<AiConfig>()
            .AsQueryable()
            .AnyAsync(
                x => x.Version != null
                     && x.Version.ToLower() == normalizedVersion
                     && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<AiConfig>()
            .AsQueryable()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default)
    {
        var others = await _unitOfWork.GetRepository<AiConfig>()
            .GetAllByPropertyAsync(x => x.IsActive && x.Id != currentConfigId);

        foreach (var other in others)
        {
            other.IsActive = false;
            await _unitOfWork.GetRepository<AiConfig>().UpdateAsync(other);
        }
    }

    public async Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var pagedResult = await _unitOfWork.GetRepository<AiConfig>()
            .GetPagedAsync(pageNumber, pageSize, orderBy: q => q.OrderByDescending(config => config.CreatedAt));

        var domainItems = pagedResult.Items.Select(ToDomain).ToList();

        return new PagedResult<AiConfigModel>(domainItems, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }

    private AiConfig ToEntity(AiConfigModel model)
    {
        var entity = AiConfigMapper.ToEntity(model);
        entity.ApiKey = _aiSecretProtector.Protect(entity.ApiKey);
        return entity;
    }

    private AiConfigModel ToDomain(AiConfig entity)
    {
        var model = AiConfigMapper.ToDomain(entity);
        model.ApiKey = _aiSecretProtector.Unprotect(entity.ApiKey);
        return model;
    }
}
