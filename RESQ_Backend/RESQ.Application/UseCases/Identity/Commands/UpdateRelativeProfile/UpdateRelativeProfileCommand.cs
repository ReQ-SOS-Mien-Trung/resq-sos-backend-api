using System;
using System.Collections.Generic;
using MediatR;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRelativeProfile
{
    public record UpdateRelativeProfileCommand(
        Guid UserId,
        Guid ProfileId,
        string DisplayName,
        string? PhoneNumber,
        string PersonType,
        string RelationGroup,
        List<string>? Tags,
        string? MedicalBaselineNote,
        string? SpecialNeedsNote,
        string? SpecialDietNote,
        DateTime? UpdatedAt
    ) : IRequest<RelativeProfileResponse>;
}
