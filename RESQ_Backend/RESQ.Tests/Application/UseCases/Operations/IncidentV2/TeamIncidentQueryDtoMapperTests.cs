using RESQ.Application.UseCases.Operations.Queries.Shared;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.IncidentV2;

public class TeamIncidentQueryDtoMapperTests
{
    // -- existing test (updated for new detail_json shape) ---------------------

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

    // -- incidentType fallback tests --------------------------------------------

    [Fact]
    public void ToDto_IncidentType_ReturnsStoredSubtype_WhenNotGeneric()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, incidentType: "whole_team_stranded");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.Equal("whole_team_stranded", dto.IncidentType);
    }

    [Fact]
    public void ToDto_IncidentType_FallsBackToDetailJson_WhenStoredIsGenericMission()
    {
        var incident = BuildIncident(
            TeamIncidentScope.Mission,
            incidentType: IncidentV2Constants.MissionIncidentType,
            detailJson: "{\"incidentType\":\"lost_communication\"}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.Equal("lost_communication", dto.IncidentType);
    }

    [Fact]
    public void ToDto_IncidentType_FallsBackToDetailJson_WhenStoredIsGenericActivity()
    {
        var incident = BuildIncident(
            TeamIncidentScope.Activity,
            incidentType: IncidentV2Constants.ActivityIncidentType,
            detailJson: "{\"incidentType\":\"vehicle_damage\"}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.Equal("vehicle_damage", dto.IncidentType);
    }

    [Fact]
    public void ToDto_IncidentType_FallsBackToGeneric_WhenNoDetailJson()
    {
        var incident = BuildIncident(TeamIncidentScope.Activity, incidentType: null);

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.Equal(IncidentV2Constants.ActivityIncidentType, dto.IncidentType);
    }

    // -- hasInjuredMember tests -------------------------------------------------

    [Fact]
    public void ToDto_HasInjuredMember_True_FromNestedTeamStatusLightly()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson:
            "{\"teamStatus\":{\"lightlyInjuredMembers\":2}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasInjuredMember);
    }

    [Fact]
    public void ToDto_HasInjuredMember_True_FromNestedTeamStatusSeverely()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson:
            "{\"teamStatus\":{\"severelyInjuredMembers\":1,\"lightlyInjuredMembers\":0}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasInjuredMember);
    }

    [Fact]
    public void ToDto_HasInjuredMember_True_FromUrgentMedical()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson:
            "{\"urgentMedical\":{\"needsImmediateEmergencyCare\":true}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasInjuredMember);
    }

    [Fact]
    public void ToDto_HasInjuredMember_False_WhenAllZero()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson:
            "{\"teamStatus\":{\"lightlyInjuredMembers\":0,\"severelyInjuredMembers\":0}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.False(dto.HasInjuredMember);
    }

    // -- hasSupportRequest tests ------------------------------------------------

    [Fact]
    public void ToDto_HasSupportRequest_True_WhenRescueRequestInDetailJson()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson:
            "{\"rescueRequest\":{\"supportTypes\":[\"rescue_support\"],\"priority\":\"high\"}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasSupportRequest);
    }

    [Fact]
    public void ToDto_HasSupportRequest_True_WhenSupportRequestInDetailJson()
    {
        var incident = BuildIncident(TeamIncidentScope.Activity, detailJson:
            "{\"supportRequest\":{\"supportTypes\":[\"vehicle_support\"]}}");

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasSupportRequest);
    }

    [Fact]
    public void ToDto_HasSupportRequest_True_WhenNeedSupportSosIsTrue()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission);
        incident.NeedSupportSos = true;

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.True(dto.HasSupportRequest);
    }

    [Fact]
    public void ToDto_HasSupportRequest_False_WhenNeitherFlagNorDetailJson()
    {
        var incident = BuildIncident(TeamIncidentScope.Mission, detailJson: "{\"note\":\"ok\"}");
        incident.NeedSupportSos = false;

        var dto = TeamIncidentQueryDtoMapper.ToDto(incident, null);

        Assert.False(dto.HasSupportRequest);
    }

    // -- helper ----------------------------------------------------------------

    private static TeamIncidentModel BuildIncident(
        TeamIncidentScope scope,
        string? incidentType = null,
        string? detailJson = null)
    {
        return new TeamIncidentModel
        {
            Id = 1,
            MissionTeamId = 10,
            IncidentScope = scope,
            IncidentType = incidentType,
            Status = TeamIncidentStatus.Reported,
            DetailJson = detailJson,
            AffectedActivities = []
        };
    }
}
