using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles
{
    public class GetRelativeProfilesQueryHandler : IRequestHandler<GetRelativeProfilesQuery, List<RelativeProfileResponse>>
    {
        private readonly IRelativeProfileRepository _repository;

        public GetRelativeProfilesQueryHandler(IRelativeProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<RelativeProfileResponse>> Handle(GetRelativeProfilesQuery request, CancellationToken cancellationToken)
        {
            var models = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

            return models.Select(m => new RelativeProfileResponse
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
            }).ToList();
        }
    }
}
