using System;
using System.Collections.Generic;
using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles
{
    public record SyncRelativeProfilesCommand(
        Guid UserId,
        IList<SyncProfileItemDto> Profiles
    ) : IRequest<SyncRelativeProfilesResponse>;
}
