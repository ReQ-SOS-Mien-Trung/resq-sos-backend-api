using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplicationStatusMetadata;
using RESQ.Application.UseCases.Identity.Queries.GetRescuers;
using RESQ.Application.UseCases.Identity.Queries.GetUsersForPermission;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Tests.Application.UseCases.Identity;

public class AdminIdentityQueryHandlerTests
{
    [Fact]
    public async Task GetRescuers_Handle_ForwardsRescuerTypeFilter()
    {
        var userRepository = new RecordingUserRepository(
            new PagedResult<UserModel>(
            [
                new UserModel
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    FirstName = "Lan",
                    LastName = "Nguyen",
                    Email = "lan@example.com",
                    RoleId = 3,
                    RescuerType = RescuerType.Core,
                    IsEligibleRescuer = true
                }
            ], 1, 1, 10));
        var handler = new GetRescuersQueryHandler(
            userRepository,
            new EmptyRescuerScoreRepository(),
            new StaticRescuerScoreVisibilityConfigRepository());

        var result = await handler.Handle(
            new GetRescuersQuery(1, 10, false, "lan", RescuerType.Core),
            CancellationToken.None);

        Assert.Equal(RescuerType.Core, userRepository.LastRescuerType);
        var item = Assert.Single(result.Items);
        Assert.Equal("Core", item.RescuerType);
    }

    [Fact]
    public async Task GetUsersForPermission_Handle_ForwardsFiltersAndMapsPagedResult()
    {
        var userRepository = new RecordingUserRepository(
            new PagedResult<UserModel>(
            [
                new UserModel
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    RoleId = 4,
                    FirstName = "Lan",
                    LastName = "Nguyen",
                    Phone = "0901234567",
                    Email = "lan@example.com",
                    CreatedAt = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc)
                }
            ],
            1,
            2,
            5));
        var handler = new GetUsersForPermissionQueryHandler(userRepository);

        var result = await handler.Handle(
            new GetUsersForPermissionQuery(
                PageNumber: 2,
                PageSize: 5,
                RoleId: 4,
                Name: "Lan Nguyen",
                Phone: "090",
                Email: "lan@example.com"),
            CancellationToken.None);

        Assert.Equal(4, userRepository.LastPermissionRoleId);
        Assert.Equal("Lan Nguyen", userRepository.LastPermissionName);
        Assert.Equal("090", userRepository.LastPermissionPhone);
        Assert.Equal("lan@example.com", userRepository.LastPermissionEmail);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(1, result.TotalCount);

        var item = Assert.Single(result.Items);
        Assert.Equal("Lan", item.FirstName);
        Assert.Equal("Nguyen", item.LastName);
        Assert.Equal("0901234567", item.Phone);
        Assert.Equal("lan@example.com", item.Email);
        Assert.Equal(4, item.RoleId);
    }

    [Fact]
    public async Task GetRescuerApplications_Handle_ForwardsEnumStatusFilter()
    {
        var repository = new RecordingRescuerApplicationRepository(
            new PagedResult<RescuerApplicationListItemDto>([], 0, 1, 10));
        var handler = new GetRescuerApplicationsQueryHandler(
            repository,
            NullLogger<GetRescuerApplicationsQueryHandler>.Instance);

        await handler.Handle(
            new GetRescuerApplicationsQuery(
                PageNumber: 1,
                PageSize: 10,
                Status: RescuerApplicationStatus.Approved,
                Name: "Lan",
                Email: null,
                Phone: null,
                RescuerType: "Core"),
            CancellationToken.None);

        Assert.Equal(RescuerApplicationStatus.Approved, repository.LastStatus);
    }

    [Fact]
    public async Task GetRescuerApplicationStatusMetadata_ReturnsExpectedPairs()
    {
        var handler = new GetRescuerApplicationStatusMetadataQueryHandler();

        var result = await handler.Handle(new GetRescuerApplicationStatusMetadataQuery(), CancellationToken.None);

        Assert.Equal(
        [
            ("Pending", "Chờ duyệt"),
            ("Approved", "Đã duyệt"),
            ("Rejected", "Đã từ chối")
        ],
            result.Select(x => (x.Key, x.Value)).ToList());
    }

    private sealed class RecordingUserRepository(PagedResult<UserModel> pagedResult) : IUserRepository
    {
        public RescuerType? LastRescuerType { get; private set; }
        public int? LastPermissionRoleId { get; private set; }
        public string? LastPermissionName { get; private set; }
        public string? LastPermissionPhone { get; private set; }
        public string? LastPermissionEmail { get; private set; }

        public Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, RescuerType? rescuerType = null, CancellationToken cancellationToken = default)
        {
            LastRescuerType = rescuerType;
            return Task.FromResult(pagedResult);
        }

        public Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(UserModel user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? name = null, string? phone = null, string? email = null, CancellationToken cancellationToken = default)
        {
            LastPermissionRoleId = roleId;
            LastPermissionName = name;
            LastPermissionPhone = phone;
            LastPermissionEmail = email;
            return Task.FromResult(pagedResult);
        }
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class RecordingRescuerApplicationRepository(PagedResult<RescuerApplicationListItemDto> pagedResult) : IRescuerApplicationRepository
    {
        public RescuerApplicationStatus? LastStatus { get; private set; }

        public Task<PagedResult<RescuerApplicationListItemDto>> GetPagedAsync(int pageNumber, int pageSize, RescuerApplicationStatus? status = null, string? name = null, string? email = null, string? phone = null, string? rescuerType = null, CancellationToken cancellationToken = default)
        {
            LastStatus = status;
            return Task.FromResult(pagedResult);
        }

        public Task<RescuerApplicationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescuerApplicationModel?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescuerApplicationModel?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescuerApplicationDto?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescuerApplicationDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReplaceDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RescuerApplicationDocumentModel>> GetDocumentsByApplicationIdAsync(int applicationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmptyRescuerScoreRepository : IRescuerScoreRepository
    {
        public Task<RescuerScoreModel?> GetByRescuerIdAsync(Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IDictionary<Guid, RescuerScoreModel>> GetByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescuerScoreModel?> GetVisibleByRescuerIdAsync(Guid rescuerId, int minimumEvaluationCount, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IDictionary<Guid, RescuerScoreModel>> GetVisibleByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, int minimumEvaluationCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IDictionary<Guid, RescuerScoreModel>>(new Dictionary<Guid, RescuerScoreModel>());
        public Task RefreshAsync(IEnumerable<MissionTeamMemberEvaluationModel> newEvaluations, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StaticRescuerScoreVisibilityConfigRepository : IRescuerScoreVisibilityConfigRepository
    {
        public Task<RescuerScoreVisibilityConfigDto?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<RescuerScoreVisibilityConfigDto?>(new RescuerScoreVisibilityConfigDto { MinimumEvaluationCount = 0 });

        public Task<RescuerScoreVisibilityConfigDto> UpsertAsync(int minimumEvaluationCount, Guid updatedBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
