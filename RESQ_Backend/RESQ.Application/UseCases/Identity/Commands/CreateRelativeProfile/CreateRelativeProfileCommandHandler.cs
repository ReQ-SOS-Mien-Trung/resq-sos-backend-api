using System.Threading;
using System.Threading.Tasks;
using System;
using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile
{
    public class CreateRelativeProfileCommandHandler : IRequestHandler<CreateRelativeProfileCommand, RelativeProfileResponse>
    {
        private readonly IRelativeProfileRepository _repository;

        public CreateRelativeProfileCommandHandler(IRelativeProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<RelativeProfileResponse> Handle(CreateRelativeProfileCommand request, CancellationToken cancellationToken)
        {
            var (displayName, phoneNumber, personType, relationGroup, tagsJson,
                 medicalBaselineNote, specialNeedsNote, specialDietNote, gender, medicalProfileJson) =
                RelativeProfileNormalizer.Normalize(
                    request.DisplayName,
                    request.PhoneNumber,
                    request.PersonType,
                    request.RelationGroup,
                    request.Tags,
                    request.MedicalBaselineNote,
                    request.SpecialNeedsNote,
                    request.SpecialDietNote,
                    request.Gender,
                    request.MedicalProfileJson);

            var now = DateTime.UtcNow;
            var model = new UserRelativeProfileModel
            {
                Id = request.ClientId ?? Guid.NewGuid(),
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
                MedicalProfileJson = medicalProfileJson,
                ProfileUpdatedAt = request.UpdatedAt ?? now,
                CreatedAt = now,
                UpdatedAt = now
            };

            var created = await _repository.CreateAsync(model, cancellationToken);

            return new RelativeProfileResponse
            {
                Id = created.Id,
                UserId = created.UserId,
                DisplayName = created.DisplayName,
                PhoneNumber = created.PhoneNumber,
                PersonType = created.PersonType,
                RelationGroup = created.RelationGroup,
                Tags = RelativeProfileNormalizer.DeserializeTags(created.TagsJson),
                MedicalBaselineNote = created.MedicalBaselineNote,
                SpecialNeedsNote = created.SpecialNeedsNote,
                SpecialDietNote = created.SpecialDietNote,
                Gender = created.Gender,
                MedicalProfile = RelativeProfileNormalizer.DeserializeMedicalProfile(created.MedicalProfileJson),
                ProfileUpdatedAt = created.ProfileUpdatedAt,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };
        }
    }
}
