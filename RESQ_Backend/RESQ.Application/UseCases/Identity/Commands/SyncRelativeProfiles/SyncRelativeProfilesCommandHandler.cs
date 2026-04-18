using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles
{
    public class SyncRelativeProfilesCommandHandler : IRequestHandler<SyncRelativeProfilesCommand, SyncRelativeProfilesResponse>
    {
        private readonly IRelativeProfileRepository _repository;

        public SyncRelativeProfilesCommandHandler(IRelativeProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<SyncRelativeProfilesResponse> Handle(SyncRelativeProfilesCommand request, CancellationToken cancellationToken)
        {
            // Validate no duplicate ids in payload
            var ids = request.Profiles.Select(p => p.Id).ToList();
            if (ids.Count != ids.Distinct().Count())
                throw new BadRequestException("Payload sync chứa id trùng nhau.");

            var now = DateTime.UtcNow;

            // Normalize and map all items to domain models
            var models = request.Profiles.Select(item =>
            {
                var medicalProfileJson = item.MedicalProfile != null
                    ? JsonSerializer.Serialize(item.MedicalProfile)
                    : null;

                var (displayName, phoneNumber, personType, relationGroup, tagsJson,
                     medicalBaselineNote, specialNeedsNote, specialDietNote, gender, normalizedMedicalProfileJson) =
                    RelativeProfileNormalizer.Normalize(
                        item.DisplayName,
                        item.PhoneNumber,
                        item.PersonType,
                        item.RelationGroup,
                        item.Tags,
                        item.MedicalBaselineNote,
                        item.SpecialNeedsNote,
                        item.SpecialDietNote,
                        item.Gender,
                        medicalProfileJson);

                return new UserRelativeProfileModel
                {
                    Id = item.Id,
                    UserId = request.UserId,
                    DisplayName = displayName,
                    PhoneNumber = phoneNumber,
                    PersonType = personType,
                    RelationGroup = relationGroup,
                    TagsJson = tagsJson,
                    MedicalBaselineNote = medicalBaselineNote,
                    SpecialNeedsNote = specialNeedsNote,
                    SpecialDietNote = specialDietNote,
                    Gender = gender,
                    MedicalProfileJson = normalizedMedicalProfileJson,
                    ProfileUpdatedAt = item.UpdatedAt,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }).ToList();

            var (created, updated, deleted) = await _repository.ReplaceAllForUserAsync(
                request.UserId, models, cancellationToken);

            var resultProfiles = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

            return new SyncRelativeProfilesResponse
            {
                Profiles = resultProfiles.Select(m => new RelativeProfileResponse
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    DisplayName = m.DisplayName,
                    PhoneNumber = m.PhoneNumber,
                    PersonType = m.PersonType,
                    RelationGroup = m.RelationGroup,
                    Tags = RelativeProfileNormalizer.DeserializeTags(m.TagsJson),
                    MedicalBaselineNote = m.MedicalBaselineNote,
                    SpecialNeedsNote = m.SpecialNeedsNote,
                    SpecialDietNote = m.SpecialDietNote,
                    Gender = m.Gender,
                    MedicalProfile = RelativeProfileNormalizer.DeserializeMedicalProfile(m.MedicalProfileJson),
                    ProfileUpdatedAt = m.ProfileUpdatedAt,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                }).ToList(),
                CreatedCount = created,
                UpdatedCount = updated,
                DeletedCount = deleted,
                SyncedAt = now
            };
        }
    }
}
