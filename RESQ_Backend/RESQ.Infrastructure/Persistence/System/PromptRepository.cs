using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Infrastructure.Persistence.System;

public class PromptRepository(IUnitOfWork unitOfWork) : IPromptRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PromptModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Prompt>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false);

        return entity == null ? null : PromptMapper.ToDomain(entity);
    }

    public async Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Prompt>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : PromptMapper.ToDomain(entity);
    }

    public async Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
    {
        var entity = PromptMapper.ToEntity(prompt);
        await _unitOfWork.GetRepository<Prompt>().AddAsync(entity);
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Prompt>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false);

        return entity != null;
    }
}
