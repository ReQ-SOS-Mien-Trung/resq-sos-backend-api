using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Mappers.Identity
{
    public static class UserRelativeProfileMapper
    {
        public static UserRelativeProfile ToEntity(UserRelativeProfileModel model)
        {
            return new UserRelativeProfile
            {
                Id = model.Id,
                UserId = model.UserId,
                DisplayName = model.DisplayName,
                PhoneNumber = model.PhoneNumber,
                PersonType = model.PersonType,
                RelationGroup = model.RelationGroup,
                Gender = model.Gender,
                TagsJson = model.TagsJson,
                MedicalBaselineNote = model.MedicalBaselineNote,
                SpecialNeedsNote = model.SpecialNeedsNote,
                SpecialDietNote = model.SpecialDietNote,
                MedicalProfileJson = model.MedicalProfileJson,
                ProfileUpdatedAt = model.ProfileUpdatedAt,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt
            };
        }

        public static UserRelativeProfileModel ToModel(UserRelativeProfile entity)
        {
            return new UserRelativeProfileModel
            {
                Id = entity.Id,
                UserId = entity.UserId,
                DisplayName = entity.DisplayName,
                PhoneNumber = entity.PhoneNumber,
                PersonType = entity.PersonType,
                RelationGroup = entity.RelationGroup,
                Gender = entity.Gender,
                TagsJson = entity.TagsJson,
                MedicalBaselineNote = entity.MedicalBaselineNote,
                SpecialNeedsNote = entity.SpecialNeedsNote,
                SpecialDietNote = entity.SpecialDietNote,
                MedicalProfileJson = entity.MedicalProfileJson,
                ProfileUpdatedAt = entity.ProfileUpdatedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public static void UpdateEntity(UserRelativeProfile entity, UserRelativeProfileModel model)
        {
            entity.DisplayName = model.DisplayName;
            entity.PhoneNumber = model.PhoneNumber;
            entity.PersonType = model.PersonType;
            entity.RelationGroup = model.RelationGroup;
            entity.Gender = model.Gender;
            entity.TagsJson = model.TagsJson;
            entity.MedicalBaselineNote = model.MedicalBaselineNote;
            entity.SpecialNeedsNote = model.SpecialNeedsNote;
            entity.SpecialDietNote = model.SpecialDietNote;
            entity.MedicalProfileJson = model.MedicalProfileJson;
            entity.ProfileUpdatedAt = model.ProfileUpdatedAt;
            entity.UpdatedAt = model.UpdatedAt;
        }
    }
}
