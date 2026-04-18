using System.Collections.Generic;

namespace RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles
{
    public class SyncRelativeProfilesRequestDto
    {
        public List<SyncProfileItemDto> Profiles { get; set; } = new();
    }
}
