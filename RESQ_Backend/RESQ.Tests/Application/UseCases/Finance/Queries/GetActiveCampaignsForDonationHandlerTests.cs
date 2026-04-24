using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetActiveCampaignsForDonation;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Tests.Application.UseCases.Finance.Queries;

public class GetActiveCampaignsForDonationHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsActiveCampaignsForDonationSelection()
    {
        var campaigns = new List<FundCampaignModel>
        {
            FundCampaignModel.Reconstitute(
                id: 1,
                code: "CP-001",
                name: "Campaign A",
                region: "Da Nang",
                startDate: new DateOnly(2026, 4, 1),
                endDate: new DateOnly(2026, 5, 1),
                targetAmount: 1000,
                totalAmount: 250,
                currentBalance: 200,
                status: FundCampaignStatus.Active,
                suspendReason: null,
                createdBy: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                lastModifiedBy: null,
                lastModifiedAt: null,
                isDeleted: false),
            FundCampaignModel.Reconstitute(
                id: 2,
                code: "CP-002",
                name: "Campaign B",
                region: "Hue",
                startDate: new DateOnly(2026, 4, 10),
                endDate: new DateOnly(2026, 5, 10),
                targetAmount: 5000,
                totalAmount: 1200,
                currentBalance: 900,
                status: FundCampaignStatus.Active,
                suspendReason: null,
                createdBy: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                lastModifiedBy: null,
                lastModifiedAt: null,
                isDeleted: false)
        };

        var handler = new GetActiveCampaignsForDonationHandler(new StubFundCampaignRepository(campaigns));

        var result = await handler.Handle(new GetActiveCampaignsForDonationQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Campaign A", result[0].Name);
        Assert.Equal("CP-001", result[0].Code);
        Assert.Equal(new DateOnly(2026, 5, 1), result[0].CampaignEndDate);
        Assert.Equal(900, result[1].CurrentBalance);
    }

    private sealed class StubFundCampaignRepository(List<FundCampaignModel> activeCampaigns) : IFundCampaignRepository
    {
        public Task<List<FundCampaignModel>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(activeCampaigns);

        public Task<FundCampaignModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FundCampaignModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, List<FundCampaignStatus>? statuses = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<FundCampaignModel>> GetExpiredActiveAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
