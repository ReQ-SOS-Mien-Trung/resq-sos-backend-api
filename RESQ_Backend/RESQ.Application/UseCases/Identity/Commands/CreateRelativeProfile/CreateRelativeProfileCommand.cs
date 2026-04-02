using System;
using System.Collections.Generic;
using MediatR;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile
{
    public record CreateRelativeProfileCommand(
        Guid UserId,
        Guid? ClientId,
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
