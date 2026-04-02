using System;
using System.Collections.Generic;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;

namespace RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles
{
    public class SyncRelativeProfilesResponse
    {
        public List<RelativeProfileResponse> Profiles { get; set; } = new();
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
