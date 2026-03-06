using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Mappers.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class AbilityRepository(IUnitOfWork unitOfWork) : IAbilityRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<AbilityModel>> GetAllAbilitiesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Ability>()
            .GetAllByPropertyAsync(includeProperties: "AbilitySubgroup.AbilityCategory");

        return entities.Select(AbilityMapper.ToDomain).ToList();
    }

    public async Task<List<UserAbilityModel>> GetUserAbilitiesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<UserAbility>()
            .GetAllByPropertyAsync(
                filter: ua => ua.UserId == userId,
                includeProperties: "Ability"
            );

        return entities.Select(AbilityMapper.ToUserAbilityDomain).ToList();
    }

    public async Task SaveUserAbilitiesAsync(Guid userId, List<UserAbilityModel> abilities, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<UserAbility>();

        // Remove all existing abilities for this user
        var existingAbilities = await repo.GetAllByPropertyAsync(
            filter: ua => ua.UserId == userId
        );

        foreach (var existing in existingAbilities)
        {
            await repo.DeleteAsync(existing.UserId, existing.AbilityId);
        }

        // Add new abilities
        if (abilities.Count > 0)
        {
            var entities = abilities.Select(AbilityMapper.ToEntity).ToList();
            await repo.AddRangeAsync(entities);
        }
    }
}
