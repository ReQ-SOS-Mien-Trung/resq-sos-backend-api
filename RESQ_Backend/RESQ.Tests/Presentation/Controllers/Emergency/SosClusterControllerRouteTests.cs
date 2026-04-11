using Microsoft.AspNetCore.Mvc;
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
}
