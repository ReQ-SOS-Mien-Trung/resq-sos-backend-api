using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Presentation.Controllers.Logistics;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Presentation.Controllers.Logistics;

public class InventoryControllerTests
{
    [Fact]
    public async Task SearchReusableItemRecords_ExcludesDecommissionedStatusFromForwardedQuery()
    {
        var response = new PagedResult<ReusableUnitDto>([], 0, 1, 20);
        var mediator = new RecordingMediator(_ => response);
        var controller = new InventoryController(
            mediator,
            new StubItemCategoryRepository(),
            new AllowAuthorizationService(),
            new StubOperationalHubService())
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

        var result = await controller.SearchReusableItemRecords(
            depotId: 3,
            itemModelId: 301,
            serialNumber: "SN-001",
            pageNumber: 1,
            pageSize: 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sentQuery = Assert.IsType<GetMyDepotReusableUnitsQuery>(Assert.Single(mediator.SentRequests));

        Assert.Equal(
            Enum.GetValues<ReusableItemStatus>().Where(status => status != ReusableItemStatus.Decommissioned).ToList(),
            sentQuery.Statuses);
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

    private sealed class StubItemCategoryRepository : IItemCategoryRepository
    {
        public Task CreateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ItemCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<ItemCategoryModel?>(null);

        public Task<ItemCategoryModel?> GetByCodeAsync(ItemCategoryCode code, CancellationToken cancellationToken = default)
            => Task.FromResult<ItemCategoryModel?>(null);

        public Task<List<int>> GetIdsByCodesAsync(IReadOnlyList<ItemCategoryCode> codes, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<int>());

        public Task<List<ItemCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ItemCategoryModel>());

        public Task<PagedResult<ItemCategoryModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<ItemCategoryModel>([], 0, pageNumber, pageSize));
    }
}
