using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class DocumentFileTypeCategoryRepository(IUnitOfWork unitOfWork) : IDocumentFileTypeCategoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<DocumentFileTypeCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<DocumentFileTypeCategory>()
            .GetAllByPropertyAsync();
        return entities.Select(MapToModel).OrderBy(x => x.Id).ToList();
    }

    public async Task<DocumentFileTypeCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileTypeCategory>()
            .GetByPropertyAsync(x => x.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<DocumentFileTypeCategoryModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileTypeCategory>()
            .GetByPropertyAsync(x => x.Code == code);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<int> CreateAsync(DocumentFileTypeCategoryModel model, CancellationToken cancellationToken = default)
    {
        var entity = new DocumentFileTypeCategory
        {
            Code = model.Code,
            Description = model.Description
        };
        await _unitOfWork.GetRepository<DocumentFileTypeCategory>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(DocumentFileTypeCategoryModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileTypeCategory>()
            .GetByPropertyAsync(x => x.Id == model.Id);
        if (entity is not null)
        {
            entity.Code = model.Code;
            entity.Description = model.Description;
            await _unitOfWork.GetRepository<DocumentFileTypeCategory>().UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<DocumentFileTypeCategory>().DeleteAsyncById(id);
    }

    private static DocumentFileTypeCategoryModel MapToModel(DocumentFileTypeCategory entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Description = entity.Description
    };
}
