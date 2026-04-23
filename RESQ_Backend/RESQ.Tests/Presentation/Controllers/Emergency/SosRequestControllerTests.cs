using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsByBounds;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;
using RESQ.Domain.Enum.Emergency;
using RESQ.Presentation.Controllers.Emergency;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosRequestControllerTests
{
    [Fact]
    public async Task GetSosRequests_ForwardsBoundsFiltersToMediator()
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
        var query = new GetSosRequestsQueryParameters
        {
            MinLat = 10.70,
            MaxLat = 10.80,
            MinLng = 106.60,
            MaxLng = 106.70,
            Statuses = [SosRequestStatus.Pending, SosRequestStatus.Assigned],
            Priorities = [SosPriorityLevel.High, SosPriorityLevel.Critical],
            SosTypes = [SosRequestType.Rescue, SosRequestType.Relief]
        };

        var result = await controller.GetSosRequests(query);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetSosRequestsByBoundsQuery>(Assert.Single(mediator.SentRequests));

        Assert.Equal(10.70, sentQuery.MinLat);
        Assert.Equal(10.80, sentQuery.MaxLat);
        Assert.Equal(106.60, sentQuery.MinLng);
        Assert.Equal(106.70, sentQuery.MaxLng);
        Assert.Equal([SosRequestStatus.Pending, SosRequestStatus.Assigned], sentQuery.Statuses);
        Assert.Equal([SosPriorityLevel.High, SosPriorityLevel.Critical], sentQuery.Priorities);
        Assert.Equal([SosRequestType.Rescue, SosRequestType.Relief], sentQuery.SosTypes);
        Assert.Same(response, okResult.Value);
    }

    [Fact]
    public async Task GetSosRequests_WithoutBounds_ForwardsPagedFiltersToMediator()
    {
        var response = new GetSosRequestsPagedResponse
        {
            Items = [],
            PageNumber = 2,
            PageSize = 25,
            TotalCount = 0
        };
        var mediator = new RecordingMediator(_ => response);
        var controller = new SosRequestController(mediator, new AllowAuthorizationService());
        var query = new GetSosRequestsQueryParameters
        {
            PageNumber = 2,
            PageSize = 25,
            Statuses = [SosRequestStatus.Pending],
            Priorities = [SosPriorityLevel.Medium],
            SosTypes = [SosRequestType.Both]
        };

        var result = await controller.GetSosRequests(query);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetSosRequestsPagedQuery>(Assert.Single(mediator.SentRequests));

        Assert.Equal(2, sentQuery.PageNumber);
        Assert.Equal(25, sentQuery.PageSize);
        Assert.Equal([SosRequestStatus.Pending], sentQuery.Statuses);
        Assert.Equal([SosPriorityLevel.Medium], sentQuery.Priorities);
        Assert.Equal([SosRequestType.Both], sentQuery.SosTypes);
        Assert.Same(response, okResult.Value);
    }

    [Fact]
    public async Task GetSosRequests_WithPartialBounds_ReturnsBadRequest()
    {
        var mediator = new RecordingMediator();
        var controller = new SosRequestController(mediator, new AllowAuthorizationService());
        var query = new GetSosRequestsQueryParameters
        {
            MinLat = 10.70,
            MaxLat = 10.80
        };

        var result = await controller.GetSosRequests(query);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Empty(mediator.SentRequests);
        Assert.Contains("required for map bounds mode", badRequest.Value!.ToString());
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
