using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class OrganizationMetadataRepository(IUnitOfWork unitOfWork) : IOrganizationMetadataRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<OrganizationModel>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Organization>();
        
        var organizations = await repo.GetAllByPropertyAsync(o => o.IsActive == true);

        return organizations
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationModel
            {
                Id = o.Id,
                Name = o.Name ?? string.Empty,
                Phone = o.Phone,
                Email = o.Email,
                IsActive = o.IsActive ?? false,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .ToList();
    }

    public async Task<OrganizationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Organization>();
        
        var organization = await repo.GetByPropertyAsync(o => o.Id == id);
        
        if (organization == null) return null;

        return new OrganizationModel
        {
            Id = organization.Id,
            Name = organization.Name ?? string.Empty,
            Phone = organization.Phone,
            Email = organization.Email,
            IsActive = organization.IsActive ?? false,
            CreatedAt = organization.CreatedAt,
            UpdatedAt = organization.UpdatedAt
        };
    }

    public async Task<OrganizationModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Organization>();
        
        var organization = await repo.GetByPropertyAsync(o => o.Name == name);
        
        if (organization == null) return null;

        return new OrganizationModel
        {
            Id = organization.Id,
            Name = organization.Name ?? string.Empty,
            Phone = organization.Phone,
            Email = organization.Email,
            IsActive = organization.IsActive ?? false,
            CreatedAt = organization.CreatedAt,
            UpdatedAt = organization.UpdatedAt
        };
    }

    public async Task<OrganizationModel> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<Organization>();
        
        var organizationEntity = new Organization
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repo.AddAsync(organizationEntity);

        return new OrganizationModel
        {
            Id = organizationEntity.Id,
            Name = organizationEntity.Name ?? string.Empty,
            Phone = organizationEntity.Phone,
            Email = organizationEntity.Email,
            IsActive = organizationEntity.IsActive ?? false,
            CreatedAt = organizationEntity.CreatedAt,
            UpdatedAt = organizationEntity.UpdatedAt
        };
    }
}
