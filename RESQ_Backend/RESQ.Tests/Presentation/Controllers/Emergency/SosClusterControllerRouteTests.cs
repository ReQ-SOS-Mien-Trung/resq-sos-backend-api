using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;
using RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;
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
    public void SosClusterController_ExposesRemoveSosRequestRoute_WithExpectedResponseType()
    {
        var method = typeof(SosClusterController).GetMethod(nameof(SosClusterController.RemoveSosRequestFromCluster));

        Assert.NotNull(method);

        var httpDelete = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: false)
            .Cast<HttpDeleteAttribute>());
        Assert.Equal("{clusterId:int}/sos-requests/{sosRequestId:int}", httpDelete.Template);

        var produces = Assert.Single(method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>());
        Assert.Equal(typeof(RemoveSosRequestFromClusterResponse), produces.Type);
        Assert.Equal(StatusCodes.Status200OK, produces.StatusCode);
    }

    [Fact]
    public void SosClusterController_ExposesAddSosRequestRoute_WithExpectedResponseType_AndAuthorizationPolicy()
    {
        var method = typeof(SosClusterController).GetMethod(nameof(SosClusterController.AddSosRequestToCluster));

        Assert.NotNull(method);

        var httpPost = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
            .Cast<HttpPostAttribute>());
        Assert.Equal("{clusterId:int}/sos-requests", httpPost.Template);

        var authorize = Assert.Single(method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(PermissionConstants.PolicyMissionManage, authorize.Policy);

        var produces = Assert.Single(method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>());
        Assert.Equal(typeof(AddSosRequestToClusterResponse), produces.Type);
        Assert.Equal(StatusCodes.Status200OK, produces.StatusCode);
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
