using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;
using RESQ.Presentation.Controllers.Emergency;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosRequestControllerTests
{
    [Fact]
    public async Task GetSosRequests_ForwardsBoundsAndStatusesToMediator()
    {
        var response = new List<SosRequestDto>
        {
            new()
            {
                Id = 1,
                RawMessage = "SOS 1",
                Status = "Pending"
            }
        };
        var mediator = new RecordingMediator(_ => response);
        var controller = new SosRequestController(mediator, new AllowAuthorizationService());
        var query = new GetSosRequestsByBoundsQuery
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70,
            Statuses = ["Pending", "Assigned"]
        };

        var result = await controller.GetSosRequests(query);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetSosRequestsByBoundsQuery>(Assert.Single(mediator.SentRequests));

        Assert.Same(query, sentQuery);
        Assert.Equal(10.70, sentQuery.MinLat);
        Assert.Equal(10.80, sentQuery.MaxLat);
        Assert.Equal(106.60, sentQuery.MinLng);
        Assert.Equal(106.70, sentQuery.MaxLng);
        Assert.Equal(["Pending", "Assigned"], sentQuery.Statuses);
        Assert.Same(response, okResult.Value);
    }

    private sealed class AllowAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
