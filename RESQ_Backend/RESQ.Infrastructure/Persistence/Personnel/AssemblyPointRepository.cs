using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Domain.Entities.Personnel;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class AssemblyPointRepository(IUnitOfWork unitOfWork, ResQDbContext context) : IAssemblyPointRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResQDbContext _context = context;

    public async Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var entity = AssemblyPointMapper.ToEntity(model);
        await _unitOfWork.GetRepository<AssemblyPoint>().AddAsync(entity);
    }

    public async Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<AssemblyPoint>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == model.Id,
            tracked: true
        );

        if (existingEntity != null)
        {
            AssemblyPointMapper.UpdateEntity(existingEntity, model);
            await repository.UpdateAsync(existingEntity);
        }
    }

    // REVERTED: Standard Physical Delete
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<AssemblyPoint>().DeleteAsyncById(id);
    }

    public async Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Code == code, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<AssemblyPoint>();
        
        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        );

        var domainItems = pagedEntities.Items
            .Select(AssemblyPointMapper.ToDomain)
            .ToList();

        return new PagedResult<AssemblyPointModel>(
            domainItems,
            pagedEntities.TotalCount,
            pagedEntities.PageNumber,
            pagedEntities.PageSize
        );
    }

    public async Task<Dictionary<int, List<AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();

        var teams = await _context.RescueTeams
            .AsNoTracking()
            .Where(t => t.AssemblyPointId.HasValue && idList.Contains(t.AssemblyPointId.Value))
            .Include(t => t.RescueTeamMembers)
                .ThenInclude(m => m.User)
            .ToListAsync(cancellationToken);

        return teams
            .GroupBy(t => t.AssemblyPointId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => new AssemblyPointTeamDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    TeamType = t.TeamType,
                    Status = t.Status,
                    MaxMembers = t.MaxMembers,
                    Members = t.RescueTeamMembers.Select(m => new AssemblyPointTeamMemberDto
                    {
                        UserId = m.UserId,
                        FirstName = m.User?.FirstName,
                        LastName = m.User?.LastName,
                        AvatarUrl = m.User?.AvatarUrl,
                        RoleInTeam = m.RoleInTeam,
                        IsLeader = m.IsLeader,
                        Status = m.Status
                    }).ToList()
                }).ToList()
            );
    }
}
