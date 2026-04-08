using RESQ.Application.UseCases.Operations.Queries.Shared;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.IncidentV2;

public class TeamIncidentQueryDtoMapperTests
{
    [Fact]
    public void ToDto_DerivesFallbackIncidentTypeAndInjuryFlags()
    {
        var incident = new TeamIncidentModel
        {
            Id = 101,
            MissionTeamId = 15,
            MissionActivityId = 27,
            IncidentScope = TeamIncidentScope.Activity,
            Description = "Need support",
            Status = TeamIncidentStatus.Reported,
            NeedSupportSos = true,
            SupportSosRequestId = 88,
            DetailJson = "{" +
                "\"supportRequest\": { \"hasInjured\": true }," +
                "\"activityIds\": [27]" +
                "}",
            AffectedActivities =
            [
                new TeamIncidentAffectedActivityModel
                {
                    MissionActivityId = 27,
                    OrderIndex = 0,
                    IsPrimary = true,
                    Step = 2,
                    ActivityType = "EVACUATE",
                    Status = MissionActivityStatus.OnGoing
                }
            ]
        };

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.Equal(IncidentV2Constants.ActivityIncidentType, dto.IncidentType);
        Assert.True(dto.HasInjuredMember);
        Assert.True(dto.HasSupportRequest);
        Assert.Equal(88, dto.SupportSosRequestId);
        Assert.Single(dto.AffectedActivities);
        Assert.NotNull(dto.Detail);
    }
}