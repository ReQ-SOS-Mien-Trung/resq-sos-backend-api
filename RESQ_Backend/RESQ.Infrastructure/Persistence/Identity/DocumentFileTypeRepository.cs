using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class DocumentFileTypeRepository(IUnitOfWork unitOfWork) : IDocumentFileTypeRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<DocumentFileTypeModel>> GetAllAsync(bool? activeOnly = true, CancellationToken cancellationToken = default)
    {
        IEnumerable<DocumentFileType> entities;

        if (activeOnly == true)
        {
            entities = await _unitOfWork.GetRepository<DocumentFileType>()
                .GetAllByPropertyAsync(x => x.IsActive, includeProperties: "DocumentFileTypeCategory");
        }
        else
        {
            entities = await _unitOfWork.GetRepository<DocumentFileType>()
                .GetAllByPropertyAsync(x => true, includeProperties: "DocumentFileTypeCategory");
        }

        return entities.Select(MapToModel).OrderBy(x => x.Id).ToList();
    }

    public async Task<DocumentFileTypeModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileType>()
            .GetByPropertyAsync(x => x.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<DocumentFileTypeModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileType>()
            .GetByPropertyAsync(x => x.Code == code);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<int> CreateAsync(DocumentFileTypeModel model, CancellationToken cancellationToken = default)
    {
        var entity = new DocumentFileType
        {
            Code = model.Code,
            Name = model.Name,
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<DocumentFileType>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(DocumentFileTypeModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DocumentFileType>()
            .GetByPropertyAsync(x => x.Id == model.Id);

        if (entity is not null)
        {
            entity.Code = model.Code;
            entity.Name = model.Name;
            entity.Description = model.Description;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.GetRepository<DocumentFileType>().UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<DocumentFileType>().DeleteAsyncById(id);
    }

    private static DocumentFileTypeModel MapToModel(DocumentFileType entity)
    {
        return new DocumentFileTypeModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            DocumentFileTypeCategoryId = entity.DocumentFileTypeCategoryId,
            DocumentFileTypeCategory = entity.DocumentFileTypeCategory is not null
                ? new DocumentFileTypeCategoryModel
                {
                    Id = entity.DocumentFileTypeCategory.Id,
                    Code = entity.DocumentFileTypeCategory.Code,
                    Description = entity.DocumentFileTypeCategory.Description
                }
                : null
        };
    }
}
