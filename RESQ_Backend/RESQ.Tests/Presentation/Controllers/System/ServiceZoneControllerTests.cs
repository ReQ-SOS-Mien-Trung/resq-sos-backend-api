using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;
using RESQ.Presentation.Controllers.System;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Presentation.Controllers.System;

public class ServiceZoneControllerTests
{
    [Fact]
    public void ServiceZoneController_ExposesRootGetListRoute()
    {
        var controllerRoute = typeof(ServiceZoneController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        var method = typeof(ServiceZoneController).GetMethod(nameof(ServiceZoneController.GetAll));

        Assert.NotNull(method);
        Assert.Equal("system/service-zone", controllerRoute.Template);

        var httpGet = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());
        Assert.Null(httpGet.Template);
    }

    [Fact]
    public async Task GetAll_ForwardsQueryToMediator_AndReturnsRawListWithCounts()
    {
        var response = new List<GetServiceZoneResponse>
        {
            new()
            {
                Id = 1,
                Name = "Zone A",
                Counts = new ServiceZoneCountsDto
                {
                    PendingSosRequestCount = 10,
                    IncidentSosRequestCount = 2,
                    TeamIncidentCount = 3,
                    AssemblyPointCount = 4,
                    DepotCount = 5
                }
            }
        };
        var mediator = new RecordingMediator(_ => response);
        var controller = new ServiceZoneController(mediator);

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<GetAllServiceZoneQuery>(Assert.Single(mediator.SentRequests));
        var zones = Assert.IsType<List<GetServiceZoneResponse>>(okResult.Value);
        var zone = Assert.Single(zones);
        Assert.Equal(10, zone.Counts.PendingSosRequestCount);
        Assert.Equal(2, zone.Counts.IncidentSosRequestCount);
    }
}
