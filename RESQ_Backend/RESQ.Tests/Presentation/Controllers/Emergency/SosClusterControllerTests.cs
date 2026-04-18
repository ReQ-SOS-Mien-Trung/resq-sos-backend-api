using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
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

        var result = await controller.GetClusters(pageNumber: 2, pageSize: 5, sosRequestId: 99);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetSosClustersQuery>(Assert.Single(mediator.SentRequests));

        Assert.Equal(2, sentQuery.PageNumber);
        Assert.Equal(5, sentQuery.PageSize);
        Assert.Equal(99, sentQuery.SosRequestId);
        Assert.IsType<PagedResult<SosClusterDto>>(okResult.Value);
    }
}
