using Microsoft.AspNetCore.Mvc;
using RESQ.Presentation.Controllers.System;

namespace RESQ.Tests.Presentation.Controllers.System;

public class PromptControllerRouteTests
{
    [Fact]
    public void PromptController_ExposesPromptTypeMetadataRoute()
    {
        var getRoutes = typeof(PromptController)
            .GetMethods()
            .SelectMany(method => method
                .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
                .Cast<HttpGetAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains("metadata/prompt-types", getRoutes);
        Assert.Contains("{id}", getRoutes);
        Assert.DoesNotContain("metadata/prompt-types/test", getRoutes);
    }
}
