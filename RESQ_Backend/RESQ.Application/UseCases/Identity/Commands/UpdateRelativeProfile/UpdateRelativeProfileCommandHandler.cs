using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRelativeProfile
{
    public class UpdateRelativeProfileCommandHandler : IRequestHandler<UpdateRelativeProfileCommand, RelativeProfileResponse>
    {
        private readonly IRelativeProfileRepository _repository;

        public UpdateRelativeProfileCommandHandler(IRelativeProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<RelativeProfileResponse> Handle(UpdateRelativeProfileCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.ProfileId, request.UserId, cancellationToken);
            if (existing == null)
                throw new NotFoundException($"Relative profile {request.ProfileId} not found.");

            var (displayName, phoneNumber, personType, relationGroup, tagsJson,
                 medicalBaselineNote, specialNeedsNote, specialDietNote) =
                RelativeProfileNormalizer.Normalize(
                    request.DisplayName,
                    request.PhoneNumber,
                    request.PersonType,
                    request.RelationGroup,
                    request.Tags,
                    request.MedicalBaselineNote,
                    request.SpecialNeedsNote,
                    request.SpecialDietNote);

            var now = DateTime.UtcNow;
            existing.DisplayName = displayName;
            existing.PhoneNumber = phoneNumber;
            existing.PersonType = personType;
            existing.RelationGroup = relationGroup;
            existing.TagsJson = tagsJson;
            existing.MedicalBaselineNote = medicalBaselineNote;
            existing.SpecialNeedsNote = specialNeedsNote;
            existing.SpecialDietNote = specialDietNote;
            existing.ProfileUpdatedAt = request.UpdatedAt ?? now;
            existing.UpdatedAt = now;

            var updated = await _repository.UpdateAsync(existing, cancellationToken);

            return new RelativeProfileResponse
            {
                Id = updated.Id,
                UserId = updated.UserId,
                DisplayName = updated.DisplayName,
                PhoneNumber = updated.PhoneNumber,
                PersonType = updated.PersonType,
                RelationGroup = updated.RelationGroup,
                Tags = RelativeProfileNormalizer.DeserializeTags(updated.TagsJson),
                MedicalBaselineNote = updated.MedicalBaselineNote,
                SpecialNeedsNote = updated.SpecialNeedsNote,
                SpecialDietNote = updated.SpecialDietNote,
                ProfileUpdatedAt = updated.ProfileUpdatedAt,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };
        }
    }
}
