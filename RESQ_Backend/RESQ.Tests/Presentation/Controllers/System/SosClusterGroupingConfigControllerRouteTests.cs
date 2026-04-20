using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Presentation.Controllers.System;

namespace RESQ.Tests.Presentation.Controllers.System;

public class SosClusterGroupingConfigControllerRouteTests
{
    [Fact]
    public void SosClusterGroupingConfigController_AllowsCoordinatorToReadConfig()
    {
        var method = typeof(SosClusterGroupingConfigController).GetMethod(nameof(SosClusterGroupingConfigController.Get));

        Assert.NotNull(method);

        var httpGet = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());
        Assert.Null(httpGet.Template);

        var authorize = Assert.Single(method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(PermissionConstants.MissionGlobalManage, authorize.Policy);
    }

    [Fact]
    public void SosClusterGroupingConfigController_KeepsUpdateConfigAdminOnly()
    {
        var method = typeof(SosClusterGroupingConfigController).GetMethod(nameof(SosClusterGroupingConfigController.Upsert));

        Assert.NotNull(method);

        var httpPut = Assert.Single(method!
            .GetCustomAttributes(typeof(HttpPutAttribute), inherit: false)
            .Cast<HttpPutAttribute>());
        Assert.Null(httpPut.Template);

        var authorize = Assert.Single(method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(PermissionConstants.SystemConfigManage, authorize.Policy);
    }
}
