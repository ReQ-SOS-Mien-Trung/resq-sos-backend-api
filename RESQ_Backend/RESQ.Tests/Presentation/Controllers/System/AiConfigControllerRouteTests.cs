using Microsoft.AspNetCore.Mvc;
using RESQ.Presentation.Controllers.System;

namespace RESQ.Tests.Presentation.Controllers.System;

public class AiConfigControllerRouteTests
{
    [Fact]
    public void AiConfigController_ExposesExpectedVersioningRoutes()
    {
        var getRoutes = typeof(AiConfigController)
            .GetMethods()
            .SelectMany(method => method
                .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
                .Cast<HttpGetAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        var postRoutes = typeof(AiConfigController)
            .GetMethods()
            .SelectMany(method => method
                .GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
                .Cast<HttpPostAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains("{id}", getRoutes);
        Assert.Contains("{id:int}/versions", getRoutes);
        Assert.Contains("{id:int}/drafts", postRoutes);
        Assert.Contains("{id:int}/activate", postRoutes);
        Assert.Contains("{id:int}/rollback", postRoutes);
    }
}
