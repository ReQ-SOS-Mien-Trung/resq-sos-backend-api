using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Presentation.Controllers.Emergency;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosRequestControllerRouteTests
{
    [Fact]
    public void SosRequestController_ExposesRootGetRoute_WithRawListResponseType()
    {
        var method = typeof(SosRequestController).GetMethod(nameof(SosRequestController.GetSosRequests));

        Assert.NotNull(method);

        var httpGet = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());
        Assert.Null(httpGet.Template);

        var produces = Assert.Single(method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>());
        Assert.Equal(typeof(List<SosRequestDto>), produces.Type);
        Assert.Equal(StatusCodes.Status200OK, produces.StatusCode);
    }
}
