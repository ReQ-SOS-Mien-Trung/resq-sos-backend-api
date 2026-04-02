using System;
using System.Collections.Generic;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile
{
    public class CreateRelativeProfileRequestDto
    {
        public Guid? Id { get; set; }
        public string DisplayName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string PersonType { get; set; } = null!;
        public string RelationGroup { get; set; } = null!;
        public List<string>? Tags { get; set; }
        public string? MedicalBaselineNote { get; set; }
        public string? SpecialNeedsNote { get; set; }
        public string? SpecialDietNote { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
