using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IAbilityRepository
{
    Task<List<AbilityModel>> GetAllAbilitiesAsync(CancellationToken cancellationToken = default);
    Task<List<UserAbilityModel>> GetUserAbilitiesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveUserAbilitiesAsync(Guid userId, List<UserAbilityModel> abilities, CancellationToken cancellationToken = default);
}
