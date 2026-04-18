using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Presentation.Controllers.Emergency;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosClusterControllerRouteTests
{
    [Fact]
    public void SosClusterController_ExposesAlternativeDepotsRoute()
    {
        var getRoutes = typeof(SosClusterController)
            .GetMethods()
            .SelectMany(method => method
                .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
                .Cast<HttpGetAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains("{clusterId:int}/alternative-depots", getRoutes);
    }

    [Fact]
    public void SosClusterController_ExposesPagedRootGetRoute_WithExpectedResponseType()
    {
        var method = typeof(SosClusterController).GetMethod(nameof(SosClusterController.GetClusters));

        Assert.NotNull(method);

        var httpGet = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());
        Assert.Null(httpGet.Template);

        var produces = Assert.Single(method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>());
        Assert.Equal(typeof(PagedResult<SosClusterDto>), produces.Type);
        Assert.Equal(StatusCodes.Status200OK, produces.StatusCode);
    }
}
