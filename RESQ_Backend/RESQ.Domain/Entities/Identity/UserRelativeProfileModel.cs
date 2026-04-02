using System;

namespace RESQ.Domain.Entities.Identity
{
    public class UserRelativeProfileModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string PersonType { get; set; } = null!;
        public string RelationGroup { get; set; } = null!;
        public string TagsJson { get; set; } = "[]";
        public string? MedicalBaselineNote { get; set; }
        public string? SpecialNeedsNote { get; set; }
        public string? SpecialDietNote { get; set; }
        public DateTime ProfileUpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
