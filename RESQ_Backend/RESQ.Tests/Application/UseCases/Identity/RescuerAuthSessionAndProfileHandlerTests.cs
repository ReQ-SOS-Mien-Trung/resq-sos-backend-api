using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.LoginRescuer;
using RESQ.Application.UseCases.Identity.Commands.Logout;
using RESQ.Application.UseCases.Identity.Commands.RefreshToken;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

public class RescuerAuthSessionAndProfileHandlerTests
{
    [Fact]
    public async Task LoginRescuer_Handle_ReturnsTokensPermissionsAndPersistsRefreshToken()
    {
        var user = BuildVerifiedRescuer();
        var userRepository = new StubUserRepository(user);
        var permissionRepository = new StubPermissionRepository(["IdentitySelfView", "MissionView"]);
        var tokenService = new StubTokenService(["access-token-login"], ["refresh-token-login"]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new LoginRescuerCommandHandler(
            userRepository,
            permissionRepository,
            tokenService,
            unitOfWork,
            BuildJwtConfiguration(),
            NullLogger<LoginRescuerCommandHandler>.Instance);

        var response = await handler.Handle(
            new LoginRescuerCommand(user.Email!, "P@ssw0rd!"),
            CancellationToken.None);

        Assert.Equal("access-token-login", response.AccessToken);
        Assert.Equal("refresh-token-login", response.RefreshToken);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(user.Email, response.Email);
        Assert.Equal(3, response.RoleId);
        Assert.True(response.IsEmailVerified);
        Assert.Equal(["IdentitySelfView", "MissionView"], response.Permissions);
        Assert.Equal("refresh-token-login", user.RefreshToken);
        Assert.True(user.RefreshTokenExpiry > DateTime.UtcNow);
        Assert.Same(user, userRepository.LastUpdatedUser);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task RefreshToken_Handle_RotatesTokensAndReturnsPermissions()
    {
        var configuration = BuildJwtConfiguration();
        var user = BuildVerifiedRescuer();
        user.RefreshToken = "refresh-token-old";
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(2);

        var expiredAccessToken = CreateExpiredAccessToken(user.Id, configuration);
        var userRepository = new StubUserRepository(user);
        var permissionRepository = new StubPermissionRepository(["IdentitySelfView"]);
        var tokenService = new StubTokenService(["access-token-refreshed"], ["refresh-token-refreshed"]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new RefreshTokenCommandHandler(
            userRepository,
            permissionRepository,
            tokenService,
            unitOfWork,
            configuration,
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var response = await handler.Handle(
            new RefreshTokenCommand(expiredAccessToken, "refresh-token-old"),
            CancellationToken.None);

        Assert.Equal("access-token-refreshed", response.AccessToken);
        Assert.Equal("refresh-token-refreshed", response.RefreshToken);
        Assert.Equal(["IdentitySelfView"], response.Permissions);
        Assert.Equal("refresh-token-refreshed", user.RefreshToken);
        Assert.True(user.RefreshTokenExpiry > DateTime.UtcNow);
        Assert.Same(user, userRepository.LastUpdatedUser);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Logout_Handle_ClearsRefreshTokenAndPersistsChange()
    {
        var user = BuildVerifiedRescuer();
        user.RefreshToken = "refresh-token-login";
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        var userRepository = new StubUserRepository(user);
        var unitOfWork = new StubUnitOfWork();
        var handler = new LogoutCommandHandler(
            userRepository,
            unitOfWork,
            NullLogger<LogoutCommandHandler>.Instance);

        var response = await handler.Handle(new LogoutCommand(user.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Same(user, userRepository.LastUpdatedUser);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task GetCurrentUser_Handle_ReturnsProfileDepotDocumentsAndScore()
    {
        var user = BuildVerifiedRescuer();
        user.FirstName = "Lan";
        user.LastName = "Nguyen";
        user.Phone = "0901234567";
        user.RescuerType = RescuerType.Volunteer;
        user.IsEligibleRescuer = true;
        user.RescuerStep = 2;
        user.AvatarUrl = "https://cdn.example/avatar.png";
        user.Latitude = 16.047079;
        user.Longitude = 108.20623;
        user.CreatedAt = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        user.UpdatedAt = new DateTime(2026, 4, 9, 9, 0, 0, DateTimeKind.Utc);

        var application = new RescuerApplicationModel { Id = 12, UserId = user.Id };
        var document = new RescuerApplicationDocumentModel
        {
            Id = 100,
            ApplicationId = 12,
            FileUrl = "https://cdn.example/certifications/basic.pdf",
            FileTypeId = 7,
            FileTypeCode = "FIRST_AID",
            FileTypeName = "First Aid Certificate",
            UploadedAt = new DateTime(2026, 4, 2, 7, 0, 0, DateTimeKind.Utc)
        };
        var score = new RescuerScoreModel
        {
            UserId = user.Id,
            ResponseTimeScore = 8.5m,
            RescueEffectivenessScore = 9.0m,
            DecisionHandlingScore = 8.8m,
            SafetyMedicalSkillScore = 9.2m,
            TeamworkCommunicationScore = 8.9m,
            OverallAverageScore = 8.88m,
            EvaluationCount = 6,
            UpdatedAt = new DateTime(2026, 4, 10, 6, 0, 0, DateTimeKind.Utc)
        };

        var handler = new GetCurrentUserQueryHandler(
            new StubUserRepository(user),
            new StubRescuerApplicationRepository(application, [document]),
            new StubPermissionRepository(["IdentitySelfView", "MissionView"]),
            NullLogger<GetCurrentUserQueryHandler>.Instance,
            new StubDepotInventoryRepository { ActiveDepotId = 5 },
            new StubDepotRepository(new DepotModel { Id = 5, Name = "Kho Da Nang" }),
            new StubRescuerScoreRepository(score),
            new StubRescuerScoreVisibilityConfigRepository(3),
            new StubManagerDepotAccessService(new List<RESQ.Application.Services.ManagedDepotDto> { new() { DepotId = 5, DepotName = "Kho Da Nang" } }));

        var response = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        Assert.Equal(user.Id, response.Id);
        Assert.Equal("Lan", response.FirstName);
        Assert.Equal("Nguyen", response.LastName);
        Assert.Equal("rescuer@example.com", response.Email);
        Assert.Equal("Volunteer", response.RescuerType);
        Assert.True(response.IsEmailVerified);
        Assert.True(response.IsEligibleRescuer);
        Assert.Equal(2, response.RescuerStep);
        Assert.Equal(["IdentitySelfView", "MissionView"], response.Permissions);
        var managedDepot = Assert.Single(response.ManagedDepots);
        Assert.Equal(5, managedDepot.DepotId);
        Assert.Equal("Kho Da Nang", managedDepot.DepotName);

        var responseDocument = Assert.Single(response.RescuerApplicationDocuments);
        Assert.Equal(100, responseDocument.Id);
        Assert.Equal("FIRST_AID", responseDocument.FileTypeCode);

        Assert.NotNull(response.RescuerScore);
        Assert.Equal(8.88m, response.RescuerScore!.OverallAverageScore);
        Assert.Equal(6, response.RescuerScore.EvaluationCount);
    }

    private static UserModel BuildVerifiedRescuer()
        => new()
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Email = "rescuer@example.com",
            Username = "rescuer@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
            RoleId = 3,
            IsEmailVerified = true,
            RescuerStep = 1
        };

    private static IConfiguration BuildJwtConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "super-secret-key-for-unit-tests-1234567890",
                ["JwtSettings:Issuer"] = "resq-tests",
                ["JwtSettings:Audience"] = "resq-tests-clients",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

    private static string CreateExpiredAccessToken(Guid userId, IConfiguration configuration)
    {
        var secretKey = configuration["JwtSettings:SecretKey"]!;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ]),
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            Expires = DateTime.UtcNow.AddMinutes(-5),
            Issuer = configuration["JwtSettings:Issuer"],
            Audience = configuration["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    }

    private sealed class StubUserRepository(UserModel? seededUser = null) : IUserRepository
    {
        private readonly Dictionary<Guid, UserModel> _usersById = seededUser is null
            ? []
            : new Dictionary<Guid, UserModel> { [seededUser.Id] = seededUser };

        public UserModel? LastUpdatedUser { get; private set; }

        public Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => string.Equals(user.Phone, phone, StringComparison.OrdinalIgnoreCase)));

        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.GetValueOrDefault(id));

        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Where(pair => ids.Contains(pair.Key)).Select(pair => pair.Value).ToList());

        public Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => user.EmailVerificationToken == token));

        public Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<UserModel?>(null);

        public Task CreateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            _usersById[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            LastUpdatedUser = user;
            _usersById[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? search = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubPermissionRepository(List<string>? effectivePermissionCodes = null) : IPermissionRepository
    {
        private readonly List<string> _effectivePermissionCodes = effectivePermissionCodes ?? [];

        public Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, int? roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_effectivePermissionCodes.ToList());

        public Task<List<PermissionModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PermissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PermissionModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CreateAsync(PermissionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(PermissionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<PermissionModel>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetUserPermissionsAsync(Guid userId, Guid grantedBy, List<int> permissionIds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubTokenService(IEnumerable<string>? accessTokens = null, IEnumerable<string>? refreshTokens = null) : ITokenService
    {
        private readonly Queue<string> _accessTokens = new(accessTokens ?? ["generated-access-token"]);
        private readonly Queue<string> _refreshTokens = new(refreshTokens ?? ["generated-refresh-token"]);

        public string GenerateAccessToken(UserModel user)
            => _accessTokens.Count > 0 ? _accessTokens.Dequeue() : "generated-access-token";

        public string GenerateRefreshToken()
            => _refreshTokens.Count > 0 ? _refreshTokens.Dequeue() : "generated-refresh-token";

        public bool ValidateRefreshToken(string refreshToken)
            => true;

        public Guid? GetUserIdFromToken(string token)
            => null;
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public int? ActiveDepotId { get; set; }

        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveDepotId);

        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveDepotId.HasValue ? new List<int> { ActiveDepotId.Value } : []);

        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes, List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword, int page, int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds, Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, Guid performedBy, int activityId, int missionId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy, List<(int ItemModelId, int Quantity)> consumableItems, List<(int ReusableItemId, string? Condition, string? Note)> reusableItems, List<(int ItemModelId, int Quantity)> legacyReusableQuantities, string? discrepancyNote, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note, DateTime? expiredDate, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId, int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task TransferClosureItemsAsync(int sourceDepotId, int targetDepotId, int closureId, int transferId,
            Guid performedBy, IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubDepotRepository(DepotModel? depotToReturn = null) : IDepotRepository
    {
        public Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AssignManagerAsync(DepotModel depot, Guid? assignedBy = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(depotToReturn?.Id == id ? depotToReturn : null);

        public Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>());
    }

    private sealed class StubRescuerApplicationRepository(
        RescuerApplicationModel? applicationToReturn = null,
        List<RescuerApplicationDocumentModel>? documentsToReturn = null) : IRescuerApplicationRepository
    {
        private readonly List<RescuerApplicationDocumentModel> _documentsToReturn = documentsToReturn ?? [];

        public Task<RescuerApplicationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RescuerApplicationModel?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(applicationToReturn?.UserId == userId ? applicationToReturn : null);

        public Task<RescuerApplicationModel?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications.RescuerApplicationDto?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications.RescuerApplicationListItemDto>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, string? name = null, string? email = null, string? phone = null, string? rescuerType = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications.RescuerApplicationDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CreateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AddDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReplaceDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RescuerApplicationDocumentModel>> GetDocumentsByApplicationIdAsync(int applicationId, CancellationToken cancellationToken = default)
            => Task.FromResult(applicationToReturn?.Id == applicationId ? _documentsToReturn.ToList() : []);
    }

    private sealed class StubRescuerScoreRepository(RescuerScoreModel? visibleScoreToReturn = null) : IRescuerScoreRepository
    {
        public Task<RescuerScoreModel?> GetByRescuerIdAsync(Guid rescuerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IDictionary<Guid, RescuerScoreModel>> GetByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RescuerScoreModel?> GetVisibleByRescuerIdAsync(Guid rescuerId, int minimumEvaluationCount, CancellationToken cancellationToken = default)
            => Task.FromResult(visibleScoreToReturn?.UserId == rescuerId && visibleScoreToReturn.EvaluationCount >= minimumEvaluationCount
                ? visibleScoreToReturn
                : null);

        public Task<IDictionary<Guid, RescuerScoreModel>> GetVisibleByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, int minimumEvaluationCount, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RefreshAsync(IEnumerable<MissionTeamMemberEvaluationModel> newEvaluations, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubRescuerScoreVisibilityConfigRepository(int minimumEvaluationCount) : IRescuerScoreVisibilityConfigRepository
    {
        public Task<RescuerScoreVisibilityConfigDto?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<RescuerScoreVisibilityConfigDto?>(new RescuerScoreVisibilityConfigDto
            {
                MinimumEvaluationCount = minimumEvaluationCount,
                UpdatedAt = DateTime.UtcNow
            });

        public Task<RescuerScoreVisibilityConfigDto> UpsertAsync(int minimumEvaluationCount, Guid updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new RescuerScoreVisibilityConfigDto
            {
                MinimumEvaluationCount = minimumEvaluationCount,
                UpdatedBy = updatedBy,
                UpdatedAt = DateTime.UtcNow
            });
    }

    private sealed class StubManagerDepotAccessService(List<RESQ.Application.Services.ManagedDepotDto>? managedDepots = null) : IManagerDepotAccessService
    {
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(managedDepots ?? new List<RESQ.Application.Services.ManagedDepotDto>());

        public Task<int?> ResolveAccessibleDepotIdAsync(Guid userId, int? requestedDepotId, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(requestedDepotId ?? managedDepots?.FirstOrDefault()?.DepotId);

        public Task EnsureDepotAccessAsync(Guid userId, int depotId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
