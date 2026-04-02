using System;
using System.Collections.Generic;
using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles
{
    public record GetRelativeProfilesQuery(Guid UserId) : IRequest<List<RelativeProfileResponse>>;
}
