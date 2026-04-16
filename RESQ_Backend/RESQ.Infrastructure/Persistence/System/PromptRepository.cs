using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Infrastructure.Persistence.System;

public class PromptRepository(
    IUnitOfWork unitOfWork,
    IPromptSecretProtector promptSecretProtector) : IPromptRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPromptSecretProtector _promptSecretProtector = promptSecretProtector;

    public async Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
    {
        var typeStr = promptType.ToString().ToLower();
        var entity = await _unitOfWork.GetRepository<Prompt>()
            .AsQueryable()
            .Where(x => x.PromptType.ToLower() == typeStr && x.IsActive)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Prompt>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(prompt);
        await _unitOfWork.GetRepository<Prompt>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        prompt.Id = entity.Id;
    }

    public async Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(prompt);
        await _unitOfWork.GetRepository<Prompt>().UpdateAsync(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<Prompt>().DeleteAsyncById(id);
    }

    public async Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = name.Trim().ToLower();

        return await _unitOfWork.GetRepository<Prompt>()
            .AsQueryable()
            .AnyAsync(
                x => x.Name != null
                     && x.Name.ToLower() == normalizedName
                     && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalizedType = promptType.ToString().ToLower();
        var normalizedVersion = version.Trim().ToLower();

        return await _unitOfWork.GetRepository<Prompt>()
            .AsQueryable()
            .AnyAsync(
                x => x.PromptType.ToLower() == normalizedType
                     && x.Version != null
                     && x.Version.ToLower() == normalizedVersion
                     && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
    {
        var normalizedType = promptType.ToString().ToLower();
        var entities = await _unitOfWork.GetRepository<Prompt>()
            .AsQueryable()
            .Where(x => x.PromptType.ToLower() == normalizedType)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
    {
        var typeStr = promptType.ToString().ToLower();
        var others = await _unitOfWork.GetRepository<Prompt>()
            .GetAllByPropertyAsync(x => x.PromptType.ToLower() == typeStr && x.IsActive && x.Id != currentPromptId);

        foreach (var other in others)
        {
            other.IsActive = false;
            await _unitOfWork.GetRepository<Prompt>().UpdateAsync(other);
        }
    }

    public async Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var pagedResult = await _unitOfWork.GetRepository<Prompt>()
            .GetPagedAsync(pageNumber, pageSize, orderBy: q => q.OrderByDescending(p => p.CreatedAt));

        var domainItems = pagedResult.Items.Select(ToDomain).ToList();

        return new PagedResult<PromptModel>(domainItems, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }

    private Prompt ToEntity(PromptModel model)
    {
        var entity = PromptMapper.ToEntity(model);
        entity.ApiKey = _promptSecretProtector.Protect(entity.ApiKey);
        return entity;
    }

    private PromptModel ToDomain(Prompt entity)
    {
        var model = PromptMapper.ToDomain(entity);
        model.ApiKey = _promptSecretProtector.Unprotect(entity.ApiKey);
        return model;
    }
}
