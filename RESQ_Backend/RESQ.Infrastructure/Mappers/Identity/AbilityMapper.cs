using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Mappers.Identity;

public static class AbilityMapper
{
    public static AbilityModel ToDomain(Ability entity)
    {
        return new AbilityModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Description = entity.Description,
            AbilitySubgroupId = entity.AbilitySubgroupId,
            AbilitySubgroup = entity.AbilitySubgroup is not null
                ? new AbilitySubgroupModel
                {
                    Id = entity.AbilitySubgroup.Id,
                    Code = entity.AbilitySubgroup.Code,
                    Description = entity.AbilitySubgroup.Description,
                    AbilityCategoryId = entity.AbilitySubgroup.AbilityCategoryId
                }
                : null
        };
    }

    public static UserAbilityModel ToUserAbilityDomain(UserAbility entity)
    {
        return new UserAbilityModel
        {
            UserId = entity.UserId,
            AbilityId = entity.AbilityId,
            Level = entity.Level,
            AbilityCode = entity.Ability?.Code,
            AbilityDescription = entity.Ability?.Description
        };
    }

    public static UserAbility ToEntity(UserAbilityModel model)
    {
        return new UserAbility
        {
            UserId = model.UserId,
            AbilityId = model.AbilityId,
            Level = model.Level
        };
    }
}
