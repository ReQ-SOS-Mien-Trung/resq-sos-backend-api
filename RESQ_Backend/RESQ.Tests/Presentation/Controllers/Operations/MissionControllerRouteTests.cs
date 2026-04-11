using Microsoft.AspNetCore.Mvc;
using RESQ.Presentation.Controllers.Operations;

namespace RESQ.Tests.Presentation.Controllers.Operations;

public class MissionControllerRouteTests
{
    [Fact]
    public void MissionController_DoesNotExposePendingActivitiesPatchRoute()
    {
        var patchRoutes = typeof(MissionController)
            .GetMethods()
            .SelectMany(method => method
                .GetCustomAttributes(typeof(HttpPatchAttribute), inherit: false)
                .Cast<HttpPatchAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.DoesNotContain("{missionId:int}/activities/pending", patchRoutes);
        Assert.Contains("{missionId:int}/activities/{activityId:int}/status", patchRoutes);
    }
}
