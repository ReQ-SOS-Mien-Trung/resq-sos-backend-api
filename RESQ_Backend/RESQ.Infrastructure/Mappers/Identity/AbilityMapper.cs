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
                    AbilityCategoryId = entity.AbilitySubgroup.AbilityCategoryId,
                    AbilityCategory = entity.AbilitySubgroup.AbilityCategory is not null
                        ? new AbilityCategoryModel
                        {
                            Id = entity.AbilitySubgroup.AbilityCategory.Id,
                            Code = entity.AbilitySubgroup.AbilityCategory.Code,
                            Description = entity.AbilitySubgroup.AbilityCategory.Description
                        }
                        : null
                }
                : null
        };
    }

    public static UserAbilityModel ToUserAbilityDomain(UserAbility entity)
    {
        var subgroup = entity.Ability?.AbilitySubgroup;
        var category = subgroup?.AbilityCategory;
        return new UserAbilityModel
        {
            UserId = entity.UserId,
            AbilityId = entity.AbilityId,
            Level = entity.Level,
            AbilityCode = entity.Ability?.Code,
            AbilityDescription = entity.Ability?.Description,
            SubgroupId = subgroup?.Id,
            SubgroupCode = subgroup?.Code,
            SubgroupDescription = subgroup?.Description,
            CategoryId = category?.Id,
            CategoryCode = category?.Code,
            CategoryDescription = category?.Description
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
