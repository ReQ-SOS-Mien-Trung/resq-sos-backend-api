using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;
using RESQ.Presentation.Controllers.Emergency;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosRequestControllerRouteTests
{
    [Fact]
    public void SosRequestController_ExposesRootGetRoute_WithMapAndPagedResponseTypes()
    {
        var method = typeof(SosRequestController).GetMethod(nameof(SosRequestController.GetSosRequests));

        Assert.NotNull(method);

        var httpGet = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());
        Assert.Null(httpGet.Template);

        var produces = method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>()
            .ToList();

        Assert.Contains(produces, attribute =>
            attribute.Type == typeof(List<SosRequestDto>)
            && attribute.StatusCode == StatusCodes.Status200OK);
        Assert.Contains(produces, attribute =>
            attribute.Type == typeof(GetSosRequestsPagedResponse)
            && attribute.StatusCode == StatusCodes.Status200OK);
        Assert.Contains(produces, attribute => attribute.StatusCode == StatusCodes.Status400BadRequest);
    }
}
