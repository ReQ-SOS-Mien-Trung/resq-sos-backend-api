using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Presentation.Controllers.Emergency;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Presentation.Controllers.Emergency;

public class SosClusterControllerTests
{
    [Fact]
    public async Task GetClusters_ForwardsPagingAndSosRequestFilterToMediator()
    {
        var mediator = new RecordingMediator(request =>
        {
            return new PagedResult<SosClusterDto>([], 0, 2, 5);
        });
        var controller = new SosClusterController(mediator);

        var statuses = new List<string> { "Pending", "Suggested" };

        var result = await controller.GetClusters(
            pageNumber: 2,
            pageSize: 5,
            sosRequestId: 99,
            statuses: statuses);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetSosClustersQuery>(Assert.Single(mediator.SentRequests));

        Assert.Equal(2, sentQuery.PageNumber);
        Assert.Equal(5, sentQuery.PageSize);
        Assert.Equal(99, sentQuery.SosRequestId);
        Assert.Same(statuses, sentQuery.Statuses);
        Assert.IsType<PagedResult<SosClusterDto>>(okResult.Value);
    }

    [Fact]
    public async Task RemoveSosRequestFromCluster_ForwardsRouteValuesAndUserIdToMediator()
    {
        var response = new RemoveSosRequestFromClusterResponse
        {
            ClusterId = 7,
            RemovedSosRequestId = 101,
            IsClusterDeleted = false
        };
        var mediator = new RecordingMediator(_ => response);
        var controller = new SosClusterController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "aaaaaaaa-1111-1111-1111-111111111111")
                        ],
                        "test"))
                }
            }
        };

        var result = await controller.RemoveSosRequestFromCluster(7, 101);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentCommand = Assert.IsType<RemoveSosRequestFromClusterCommand>(Assert.Single(mediator.SentRequests));

        Assert.Equal(7, sentCommand.ClusterId);
        Assert.Equal(101, sentCommand.SosRequestId);
        Assert.Equal(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), sentCommand.RequestedByUserId);
        Assert.Same(response, okResult.Value);
    }
}
