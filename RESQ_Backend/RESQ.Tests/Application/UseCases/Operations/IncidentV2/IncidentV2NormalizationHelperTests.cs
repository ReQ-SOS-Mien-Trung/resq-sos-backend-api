using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Tests.Application.UseCases.Operations.IncidentV2;

public class IncidentV2NormalizationHelperTests
{
    [Fact]
    public void NormalizeMissionRequest_RejectsContinueMissionWithSupportRequest()
    {
        var request = new MissionIncidentReportRequest
        {
            MissionDecision = IncidentV2Constants.MissionDecisionCodes.ContinueMission,
            NeedSupportSos = true,
            SupportRequest = new IncidentSupportRequestData()
        };

        var exception = Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(request));

        Assert.Contains("continue_mission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMissionRequest_RequiresSupportRequestForRescueDecision()
    {
        var request = new MissionIncidentReportRequest
        {
            MissionDecision = IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately,
            TeamCondition = new MissionIncidentTeamConditionDto
            {
                HasInjuredMember = true
            }
        };

        var exception = Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(request));

        Assert.Contains("SupportRequest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeActivityRequest_AddsTakeoverSupport_WhenReassignRequested()
    {
        var request = new ActivityIncidentReportRequest
        {
            ActivityIds = [11, 13],
            NeedReassignActivity = true,
            CanContinueActivity = true
        };

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(request);

        Assert.True(normalized.NeedSupportSos);
        Assert.Equal(IncidentV2Constants.ActivityDecisionCodes.ReassignActivity, normalized.DecisionCode);
        Assert.NotNull(normalized.SupportRequest);
        Assert.Contains(
            IncidentV2Constants.SupportTypes.TakeoverActivity,
            normalized.SupportRequest!.SupportTypes,
            StringComparer.OrdinalIgnoreCase);
        Assert.False(normalized.ShouldFailSelectedActivities);
    }
}