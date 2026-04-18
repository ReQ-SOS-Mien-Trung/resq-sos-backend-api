using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Npgsql;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private const string MarkerName = "demo-seed-v1-2026-04-16";
    private const int TotalRescuerCount = 140;
    private const int RecentRescuerCount = 20;
    private const int UnassignedRescuerCount = 40;
    private const int EligibleAssignedRescuerCount = 78;
    private const int HueStadiumUnclusteredSosCount = 10;
    private const int HueStadiumCheckedInStandbyRescuerCount = 10;
    private const string DepotClosureTestDepotName = "Ủy ban MTTQVN Tỉnh Nghệ An";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ResQDbContext _db;
    private readonly SeedDataOptions _options;
    private readonly DemoSeedValidator _validator;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ResQDbContext db,
        IOptions<SeedDataOptions> options,
        DemoSeedValidator validator,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _options = options.Value;
        _validator = validator;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsDemoProfile)
        {
            return;
        }

        await EnsurePostGisExtensionAsync(cancellationToken);

        if (await _db.SystemMigrationAudits.AnyAsync(a => a.MigrationName == MarkerName, cancellationToken))
        {
            _logger.LogInformation("Runtime demo seed skipped because marker {MarkerName} already exists.", MarkerName);
            return;
        }

        if (await HasOperationalDataAsync(cancellationToken))
        {
            _db.SystemMigrationAudits.Add(new SystemMigrationAudit
            {
                MigrationName = MarkerName,
                AppliedAt = DateTime.UtcNow,
                Notes = "Runtime demo seed skipped because operational data already existed."
            });
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Runtime demo seed marker was added without seeding because operational data already exists.");
            return;
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            IDbContextTransaction? transaction = null;
            var ownsTransaction = false;
            try
            {
                if (_db.Database.IsRelational() && _db.Database.CurrentTransaction is null)
                {
                    transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                    ownsTransaction = true;
                }

                var seed = CreateContext();

                await SeedStaticConfigAsync(seed, cancellationToken);
                await SeedIdentityAsync(seed, cancellationToken);
                await SeedPersonnelAsync(seed, cancellationToken);
                await SeedLogisticsCatalogAsync(seed, cancellationToken);
                await SeedDepotsAndInventoryAsync(seed, cancellationToken);
                await SeedEmergencyAsync(seed, cancellationToken);
                await SeedMissionsAsync(seed, cancellationToken);
                await SeedChatAsync(seed, cancellationToken);
                await SeedSupplyRequestsAsync(seed, cancellationToken);
                await SeedFinanceAsync(seed, cancellationToken);
                await SeedAuditAndHistoryAsync(seed, cancellationToken);

                var validationErrors = await _validator.ValidateAsync(_db, cancellationToken);
                if (validationErrors.Count > 0)
                {
                    var message = "Runtime demo seed validation failed: " + string.Join(" | ", validationErrors);
                    if (_options.FailOnValidationError)
                    {
                        throw new InvalidOperationException(message);
                    }

                    _logger.LogWarning("{Message}", message);
                }

                _db.SystemMigrationAudits.Add(new SystemMigrationAudit
                {
                    MigrationName = MarkerName,
                    AppliedAt = DateTime.UtcNow,
                    Notes = $"SeedData profile={_options.Profile}; anchor={_options.AnchorDate:yyyy-MM-dd}; randomSeed={_options.RandomSeed}"
                });
                await _db.SaveChangesAsync(cancellationToken);

                if (ownsTransaction && transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                _logger.LogInformation("Runtime demo seed completed with marker {MarkerName}.", MarkerName);
            }
            finally
            {
                if (ownsTransaction && transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        });
    }

    private async Task EnsurePostGisExtensionAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            return;
        }

        var providerName = _db.Database.ProviderName;
        if (string.IsNullOrWhiteSpace(providerName)
            || !providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Dùng ADO.NET trực tiếp để tránh NpgsqlRetryingExecutionStrategy conflict
        var connection = _db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsqlConn)
            return;

        var wasClosed = npgsqlConn.State == ConnectionState.Closed;
        if (wasClosed)
            await npgsqlConn.OpenAsync(cancellationToken);

        try
        {
            // Kiểm tra PostGIS đã tồn tại chưa
            bool hasPostGis;
            await using (var checkCmd = npgsqlConn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis');";
                var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
                hasPostGis = result is true;
            }

            if (hasPostGis)
            {
                await npgsqlConn.ReloadTypesAsync();
                return;
            }

            // Thử tạo extension
            try
            {
                await using var createCmd = npgsqlConn.CreateCommand();
                createCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
                await npgsqlConn.ReloadTypesAsync();
            }
            catch (Exception ex)
            {
                // Neon.tech / managed PostgreSQL thường đã cài sẵn PostGIS nhưng không cho phép
                // CREATE EXTENSION (superuser only). Log warning thay vì crash startup.
                _logger.LogWarning(ex,
                    "Could not create PostGIS extension (may require superuser). " +
                    "Ensure PostGIS is pre-installed on the server if geography columns are used.");
            }
        }
        finally
        {
            if (wasClosed)
                await npgsqlConn.CloseAsync();
        }
    }

    private async Task ReloadPostgresTypesAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        var wasClosed = npgsqlConnection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await npgsqlConnection.OpenAsync(cancellationToken);
        }

        await npgsqlConnection.ReloadTypesAsync();

        if (wasClosed)
        {
            await npgsqlConnection.CloseAsync();
        }
    }

    private async Task<bool> HasOperationalDataAsync(CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(cancellationToken)
            || await _db.SosRequests.AnyAsync(cancellationToken)
            || await _db.Missions.AnyAsync(cancellationToken)
            || await _db.SupplyInventories.AnyAsync(cancellationToken)
            || await _db.FundCampaigns.AnyAsync(cancellationToken);
    }

    private DemoSeedContext CreateContext()
    {
        var anchorLocal = _options.AnchorDate.ToDateTime(TimeOnly.MinValue);
        var anchorUtc = VnToUtc(anchorLocal.AddDays(1).AddTicks(-1));
        var startUtc = VnToUtc(_options.AnchorDate.AddYears(-3).ToDateTime(TimeOnly.MinValue));

        return new DemoSeedContext
        {
            Options = _options,
            Random = new Random(_options.RandomSeed),
            AnchorUtc = anchorUtc,
            StartUtc = startUtc
        };
    }

    private async Task SeedStaticConfigAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        if (!await _db.AbilityCategories.AnyAsync(cancellationToken))
        {
            var categories = new[]
            {
                new AbilityCategory
                {
                    Code = "MEDICAL",
                    Description = "Năng lực y tế cứu hộ",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "FIRST_AID",
                            Description = "Sơ cấp cứu và xử lý chấn thương",
                            Abilities =
                            {
                                new Ability { Code = "CPR", Description = "Hồi sức tim phổi" },
                                new Ability { Code = "BLEEDING_CONTROL", Description = "Cầm máu và băng bó" },
                                new Ability { Code = "TRIAGE", Description = "Phân loại ưu tiên y tế hiện trường" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "CLINICAL_SUPPORT",
                            Description = "Chăm sóc y tế cơ bản",
                            Abilities =
                            {
                                new Ability { Code = "BLOOD_PRESSURE_MONITORING", Description = "Theo dõi huyết áp" },
                                new Ability { Code = "OBSTETRIC_SUPPORT", Description = "Hỗ trợ phụ nữ mang thai" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "RESCUE",
                    Description = "Năng lực cứu hộ hiện trường",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "WATER_RESCUE",
                            Description = "Cứu hộ vùng ngập lụt",
                            Abilities =
                            {
                                new Ability { Code = "BOAT_OPERATION", Description = "Điều khiển xuồng máy cứu hộ" },
                                new Ability { Code = "SWIFT_WATER_RESCUE", Description = "Cứu hộ dòng nước xiết" },
                                new Ability { Code = "LIFEJACKET_DEPLOYMENT", Description = "Triển khai áo phao và dây cứu sinh" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "TECHNICAL_RESCUE",
                            Description = "Cứu hộ kỹ thuật",
                            Abilities =
                            {
                                new Ability { Code = "ROPE_RESCUE", Description = "Cứu hộ dây" },
                                new Ability { Code = "CHAINSAW_USE", Description = "Sử dụng cưa máy mở đường" },
                                new Ability { Code = "COLLAPSE_SEARCH", Description = "Tìm kiếm trong khu vực sạt lở/sập đổ" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "TRANSPORT",
                    Description = "Năng lực vận chuyển",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "DRIVING",
                            Description = "Điều khiển phương tiện",
                            Abilities =
                            {
                                new Ability { Code = "TRUCK_DRIVING", Description = "Lái xe tải cứu trợ" },
                                new Ability { Code = "AMBULANCE_DRIVING", Description = "Lái xe cấp cứu" },
                                new Ability { Code = "MOTORBIKE_FIELD", Description = "Di chuyển xe máy trong khu vực ngập nhẹ" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "COMMUNICATION",
                    Description = "Liên lạc và điều phối",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "RADIO",
                            Description = "Liên lạc bộ đàm",
                            Abilities =
                            {
                                new Ability { Code = "RADIO_OPERATION", Description = "Vận hành bộ đàm" },
                                new Ability { Code = "FIELD_REPORTING", Description = "Báo cáo hiện trường" },
                                new Ability { Code = "MESH_RELAY", Description = "Chuyển tiếp dữ liệu khi mất mạng" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "LOGISTICS",
                    Description = "Tiếp vận và kho",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "WAREHOUSE",
                            Description = "Kho vận cứu trợ",
                            Abilities =
                            {
                                new Ability { Code = "FEFO_PICKING", Description = "Xuất kho theo hạn dùng" },
                                new Ability { Code = "LOAD_PLANNING", Description = "Sắp xếp tải trọng xe/xuồng" },
                                new Ability { Code = "DONATION_SORTING", Description = "Phân loại hàng quyên góp" }
                            }
                        }
                    }
                }
            };

            _db.AbilityCategories.AddRange(categories);
        }

        if (!await _db.CheckInRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.CheckInRadiusConfigs.Add(new CheckInRadiusConfig { MaxRadiusMeters = 150, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescueTeamRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.RescueTeamRadiusConfigs.Add(new RescueTeamRadiusConfig { MaxRadiusKm = 10, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescuerScoreVisibilityConfigs.AnyAsync(cancellationToken))
        {
            _db.RescuerScoreVisibilityConfigs.Add(new RescuerScoreVisibilityConfig { MinimumEvaluationCount = 3, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SosClusterGroupingConfigs.AnyAsync(cancellationToken))
        {
            _db.SosClusterGroupingConfigs.Add(new SosClusterGroupingConfig { MaximumDistanceKm = 4.5, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SupplyRequestPriorityConfigs.AnyAsync(cancellationToken))
        {
            _db.SupplyRequestPriorityConfigs.Add(new SupplyRequestPriorityConfig
            {
                UrgentMinutes = 30,
                HighMinutes = 120,
                MediumMinutes = 480,
                UpdatedAt = seed.AnchorUtc
            });
        }

        if (!await _db.ServiceZones.AnyAsync(cancellationToken))
        {
            foreach (var zone in ServiceZones(seed.AnchorUtc))
            {
                _db.ServiceZones.Add(zone);
            }
        }

        if (!await _db.SosPriorityRuleConfigs.AnyAsync(cancellationToken))
        {
            _db.SosPriorityRuleConfigs.Add(new SosPriorityRuleConfig
            {
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                IsActive = true,
                CreatedAt = seed.AnchorUtc,
                ActivatedAt = seed.AnchorUtc,
                ConfigJson = Json(new { levels = new[] { "Low", "Medium", "High", "Critical" } }),
                IssueWeightsJson = Json(new { unconscious = 5, drowning = 5, breathingDifficulty = 4, fever = 2, trauma = 4 }),
                MedicalSevereIssuesJson = Json(new[] { "unconscious", "drowning", "breathingDifficulty", "trauma" }),
                AgeWeightsJson = Json(new { child = 1.4, elderly = 1.3, adult = 1.0, pregnant = 1.35 }),
                RequestTypeScoresJson = Json(new { Rescue = 30, Relief = 18, Both = 40 }),
                SituationMultipliersJson = Json(new[]
                {
                    new { keys = new[] { "Flooding", "Stranded" }, multiplier = 1.4, severe = true },
                    new { keys = new[] { "Landslide" }, multiplier = 1.5, severe = true },
                    new { keys = new[] { "CannotMove", "Medical" }, multiplier = 1.3, severe = true }
                }),
                PriorityThresholdsJson = Json(new
                {
                    critical = new { minScore = 80 },
                    high = new { minScore = 60 },
                    medium = new { minScore = 35 },
                    low = new { minScore = 0 }
                }),
                WaterUrgencyScoresJson = Json(new { none = 0, low = 2, medium = 5, high = 8 }),
                FoodUrgencyScoresJson = Json(new { none = 0, oneDay = 3, twoDays = 6, critical = 9 }),
                BlanketUrgencyRulesJson = Json(new { elderly = 4, child = 4, coldRain = 3 }),
                ClothingUrgencyRulesJson = Json(new { soaked = 5, child = 3 }),
                VulnerabilityRulesJson = Json(new { children = 3, elderly = 3, pregnant = 4, injured = 5 }),
                VulnerabilityScoreExpressionJson = "{}",
                ReliefScoreExpressionJson = "{}",
                PriorityScoreExpressionJson = "{}",
                UpdatedAt = seed.AnchorUtc
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedIdentityAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var users = new List<User>();
        users.Add(CreateUser("admin", 1, 1, "Nguyễn", "Minh Tuấn", SeedConstants.AdminPasswordHash, Area(0), seed));

        for (var i = 0; i < 5; i++)
        {
            var name = VietnameseName(i + 3);
            users.Add(CreateUser($"coord{i + 1:00}", 2, i + 1, name.Last, name.First, SeedConstants.CoordinatorPasswordHash, Area(i), seed));
        }

        for (var i = 0; i < 8; i++)
        {
            var name = VietnameseName(i + 20);
            users.Add(CreateUser($"manager{i + 1:00}", 4, i + 1, name.Last, name.First, SeedConstants.ManagerPasswordHash, Area(i + 2), seed));
        }

        for (var i = 0; i < TotalRescuerCount; i++)
        {
            var name = VietnameseName(i + 40);
            var rescuerNumber = i + 1;
            var user = CreateUser($"rescuer{rescuerNumber:000}", 3, rescuerNumber, name.Last, name.First, SeedConstants.RescuerPasswordHash, Area(i), seed);
            if (IsRecentRescuerNumber(rescuerNumber))
            {
                var recentIndex = RecentRescuerIndex(rescuerNumber);
                var createdAt = RecentRescuerCreatedAt(seed, recentIndex);
                user.CreatedAt = createdAt;
                user.UpdatedAt = createdAt.AddHours(8 + recentIndex % 18);
                user.IsEmailVerified = true;
            }

            users.Add(user);
        }

        for (var i = 0; i < 140; i++)
        {
            var name = VietnameseName(i + 150);
            users.Add(CreateUser($"victim{i + 1:000}", 5, i + 1, name.Last, name.First, SeedConstants.VictimPasswordHash, Area(i + 4), seed));
        }

        users[^1].IsBanned = true;
        users[^1].BannedBy = users[0].Id;
        users[^1].BannedAt = seed.AnchorUtc.AddDays(-20);
        users[^1].BanReason = "Tạo nhiều SOS thử nghiệm sai sự thật";
        users[^2].IsBanned = true;
        users[^2].BannedBy = users[0].Id;
        users[^2].BannedAt = seed.AnchorUtc.AddDays(-48);
        users[^2].BanReason = "Spam chat hỗ trợ";

        var demoVictim = CreateDemoVictimWithPin(seed);
        users.Add(demoVictim);

        _db.Users.AddRange(users);
        await _db.SaveChangesAsync(cancellationToken);

        seed.Admins.Add(users[0]);
        seed.Coordinators.AddRange(users.Where(u => u.RoleId == 2));
        seed.Managers.AddRange(users.Where(u => u.RoleId == 4));
        seed.Rescuers.AddRange(users.Where(u => u.RoleId == 3));
        seed.Victims.AddRange(users.Where(u => u.RoleId == 5));

        _db.UserRelativeProfiles.AddRange(CreateDemoVictimRelativeProfiles(demoVictim.Id, seed));
        await _db.SaveChangesAsync(cancellationToken);

        var abilities = await _db.Abilities.OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var userAbilities = new List<UserAbility>();
        foreach (var rescuer in seed.Rescuers)
        {
            var index = seed.Rescuers.IndexOf(rescuer);
            var abilityCount = 2 + index % 5;
            for (var i = 0; i < abilityCount; i++)
            {
                var ability = abilities[(index * 3 + i) % abilities.Count];
                userAbilities.Add(new UserAbility
                {
                    UserId = rescuer.Id,
                    AbilityId = ability.Id,
                    Level = 2 + (index + i) % 4
                });
            }
        }

        _db.UserAbilities.AddRange(userAbilities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPersonnelAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var points = new[]
        {
            ("AP-HUE-TD-241015", "Sân vận động Tự Do (Thừa Thiên Huế)", 16.46751083681696, 107.59761456770599, "Available", 20, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774499522/SVDTD_TTH_sqdeoa.jpg"),
            ("AP-HUE-02", "Trường THCS Hương Sơ", 16.4952, 107.5860, "Available", (int?)null, (string?)null),
            ("AP-HUE-03", "Nhà văn hóa Quảng Điền", 16.5790, 107.5128, "Unavailable", (int?)null, (string?)null),
            ("AP-DNG-01", "Cung thể thao Tiên Sơn", 16.0471, 108.2188, "Available", (int?)null, (string?)null),
            ("AP-DNG-02", "Trung tâm Hòa Vang", 15.9886, 108.1210, "Available", (int?)null, (string?)null),
            ("AP-QTR-01", "Nhà văn hóa Đông Hà", 16.8175, 107.1003, "Available", (int?)null, (string?)null),
            ("AP-QTR-02", "Trường THPT Hải Lăng", 16.6766, 107.2284, "Closed", (int?)null, (string?)null),
            ("AP-QNM-01", "Trung tâm Tam Kỳ", 15.5736, 108.4740, "Available", (int?)null, (string?)null),
            ("AP-QNM-02", "Điểm tập kết Hội An", 15.8801, 108.3380, "Created", (int?)null, (string?)null),
            ("AP-QNG-01", "Trung tâm Quảng Ngãi", 15.1214, 108.8044, "Available", (int?)null, (string?)null)
        };

        foreach (var (code, name, lat, lon, status, maxCapacity, imageUrl) in points)
        {
            seed.AssemblyPoints.Add(new AssemblyPoint
            {
                Code = code,
                Name = name,
                MaxCapacity = maxCapacity ?? 90 + seed.AssemblyPoints.Count * 15,
                Status = status,
                Location = Point(lon, lat),
                CreatedAt = seed.StartUtc.AddDays(seed.AssemblyPoints.Count * 12),
                UpdatedAt = seed.AnchorUtc.AddDays(-seed.AssemblyPoints.Count),
                ImageUrl = imageUrl ?? $"https://cdn.resq.vn/assembly/{code.ToLowerInvariant()}.jpg",
                StatusReason = status == "Unavailable" ? "Đang sửa mái che và máy phát điện" : null,
                StatusChangedAt = seed.AnchorUtc.AddDays(-10 + seed.AssemblyPoints.Count),
                StatusChangedBy = seed.Coordinators[seed.AssemblyPoints.Count % seed.Coordinators.Count].Id
            });
        }

        _db.AssemblyPoints.AddRange(seed.AssemblyPoints);
        await _db.SaveChangesAsync(cancellationToken);

        var deployableRescuers = seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var standbyRescuerIds = standbyRescuers.Select(r => r.Id).ToHashSet();

        for (var i = 0; i < deployableRescuers.Count; i++)
        {
            deployableRescuers[i].AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id;
        }

        var profiles = seed.Rescuers.Select((user, index) => new RescuerProfile
        {
            UserId = user.Id,
            RescuerType = index % 4 == 0 ? "Core" : "Volunteer",
            IsEligibleRescuer = index < EligibleAssignedRescuerCount || standbyRescuerIds.Contains(user.Id),
            Step = index < EligibleAssignedRescuerCount || standbyRescuerIds.Contains(user.Id) ? 5 : 4,
            ApprovedBy = seed.Admins[0].Id,
            ApprovedAt = IsRecentRescuerNumber(index + 1)
                ? RecentRescuerApprovedAt(seed, user.CreatedAt, RecentRescuerIndex(index + 1))
                : seed.StartUtc.AddDays(20 + index)
        }).ToList();

        _db.RescuerProfiles.AddRange(profiles);
        await _db.SaveChangesAsync(cancellationToken);

        var applications = new List<RescuerApplication>();
        for (var i = 0; i < 45; i++)
        {
            var approved = i < 35;
            var rejected = i >= 40;
            var userId = approved ? seed.Rescuers[i].Id : seed.Victims[i].Id;
            var submitted = seed.StartUtc.AddDays(50 + i * 9);
            applications.Add(new RescuerApplication
            {
                UserId = userId,
                Status = approved ? "Approved" : rejected ? "Rejected" : "Pending",
                SubmittedAt = submitted,
                ReviewedAt = rejected || approved ? submitted.AddDays(2 + i % 4) : null,
                ReviewedBy = rejected || approved ? seed.Admins[0].Id : null,
                AdminNote = approved ? "Đủ hồ sơ và đã xác minh kỹ năng cơ bản" : rejected ? "Thiếu giấy tờ xác minh" : null
            });
        }

        _db.RescuerApplications.AddRange(applications);
        await _db.SaveChangesAsync(cancellationToken);

        var documents = new List<RescuerApplicationDocument>();
        foreach (var application in applications)
        {
            var typeIds = new[] { 9, 5, 1 + application.Id % 4 };
            foreach (var typeId in typeIds)
            {
                documents.Add(new RescuerApplicationDocument
                {
                    ApplicationId = application.Id,
                    FileTypeId = typeId,
                    FileUrl = $"https://cdn.resq.vn/docs/application-{application.Id}-{typeId}.pdf",
                    UploadedAt = application.SubmittedAt?.AddMinutes(typeId * 7)
                });
            }
        }

        _db.RescuerApplicationDocuments.AddRange(documents);

        var scores = deployableRescuers.Take(72).Select((rescuer, index) =>
        {
            var a = 6.5m + (index % 30) / 10m;
            var b = 6.2m + (index % 25) / 10m;
            var c = 6.0m + (index % 28) / 10m;
            var d = 6.4m + (index % 24) / 10m;
            var e = 6.3m + (index % 26) / 10m;
            return new RescuerScore
            {
                UserId = rescuer.Id,
                ResponseTimeScore = a,
                RescueEffectivenessScore = b,
                DecisionHandlingScore = c,
                SafetyMedicalSkillScore = d,
                TeamworkCommunicationScore = e,
                OverallAverageScore = Math.Round((a + b + c + d + e) / 5m, 2),
                EvaluationCount = index % 26,
                CreatedAt = seed.StartUtc.AddDays(100 + index),
                UpdatedAt = seed.AnchorUtc.AddDays(-index % 40)
            };
        }).ToList();
        _db.RescuerScores.AddRange(scores);

        await SeedAssemblyEventsAsync(seed, cancellationToken);
        await SeedRescueTeamsAsync(seed, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAssemblyEventsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var deployableRescuers = GetDeployableRescuers(seed);
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var events = new List<AssemblyEvent>();
        var hueStadium = GetHueStadiumAssemblyPoint(seed);
        AssemblyEvent? activeHueEvent = null;
        var currentUtc = DateTime.UtcNow;
        var currentVietnamLocal = currentUtc.AddHours(7);

        if (hueStadium is not null)
        {
            var activeAssemblyLocal = currentVietnamLocal.Date.AddDays(1).AddHours(6).AddMinutes(30);
            var assemblyDate = VnToUtc(activeAssemblyLocal);
            var checkInDeadline = VnToUtc(activeAssemblyLocal.AddMinutes(45));
            activeHueEvent = new AssemblyEvent
            {
                AssemblyPointId = hueStadium.Id,
                AssemblyDate = assemblyDate,
                Status = "Gathering",
                CreatedBy = seed.Coordinators[0].Id,
                CreatedAt = currentUtc.AddHours(-2),
                UpdatedAt = currentUtc.AddMinutes(-5),
                CheckInDeadline = checkInDeadline
            };
            events.Add(activeHueEvent);

            foreach (var rescuer in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount))
            {
                rescuer.AssemblyPointId = hueStadium.Id;
            }
        }

        for (var i = 0; i < 44; i++)
        {
            var assemblyDate = RandomEventUtc(seed, i).AddHours(6 + i % 3);
            var checkInDeadline = assemblyDate.AddMinutes(45);
            var status = checkInDeadline <= currentUtc
                ? "Completed"
                : assemblyDate <= currentUtc
                    ? "Gathering"
                    : "Scheduled";
            events.Add(new AssemblyEvent
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                AssemblyDate = assemblyDate,
                Status = status,
                CreatedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = assemblyDate.AddHours(-8),
                UpdatedAt = status == "Completed"
                    ? assemblyDate.AddHours(8)
                    : status == "Gathering"
                        ? currentUtc.AddMinutes(-10)
                        : assemblyDate.AddHours(-2),
                CheckInDeadline = checkInDeadline
            });
        }

        _db.AssemblyEvents.AddRange(events);
        await _db.SaveChangesAsync(cancellationToken);

        var participants = new List<AssemblyParticipant>();
        if (activeHueEvent is not null)
        {
            foreach (var (rescuer, index) in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount).Select((rescuer, index) => (rescuer, index)))
            {
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = activeHueEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = "CheckedIn",
                    IsCheckedIn = true,
                    CheckInTime = activeHueEvent.AssemblyDate.AddMinutes(5 + index * 2),
                    IsCheckedOut = false,
                    CheckOutTime = null
                });
            }
        }

        foreach (var assemblyEvent in events)
        {
            if (activeHueEvent is not null && assemblyEvent.Id == activeHueEvent.Id)
            {
                continue;
            }

            for (var i = 0; i < 7; i++)
            {
                var rescuer = deployableRescuers[(assemblyEvent.Id * 11 + i) % deployableRescuers.Count];
                var absent = (assemblyEvent.Id + i) % 10 == 0;
                var late = (assemblyEvent.Id + i) % 6 == 0;
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = assemblyEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = absent ? "Absent" : "CheckedIn",
                    IsCheckedIn = !absent,
                    CheckInTime = absent ? null : assemblyEvent.AssemblyDate.AddMinutes(late ? 55 : 20 + i),
                    IsCheckedOut = !absent && assemblyEvent.Status == "Completed",
                    CheckOutTime = !absent && assemblyEvent.Status == "Completed" ? assemblyEvent.AssemblyDate.AddHours(8) : null
                });
            }
        }

        _db.AssemblyParticipants.AddRange(participants);
    }

    private async Task SeedRescueTeamsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var deployableRescuers = GetDeployableRescuers(seed);
        var statuses = new[]
        {
            "Available", "Available", "Gathering", "Available", "Gathering",
            "Available", "Gathering", "Available", "Available", "Stuck",
            "Available", "Gathering", "Available", "Gathering", "Available",
            "Available", "Available", "Unavailable", "Disbanded", "Disbanded"
        };
        var types = new[] { "Mixed", "Rescue", "Medical", "Transportation" };

        for (var i = 0; i < 20; i++)
        {
            seed.RescueTeams.Add(new RescueTeam
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                ManagedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                Code = $"RT-{Area(i).Code}-{i + 1:00}",
                Name = $"Đội {TeamName(i)} {i + 1}",
                TeamType = types[i % types.Length],
                Status = statuses[i],
                MaxMembers = i >= 17 ? 10 : 8,
                Reason = statuses[i] == "Unavailable" ? "Bảo dưỡng thiết bị và nghỉ luân phiên" : null,
                AssemblyDate = RandomEventUtc(seed, i + 80),
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i),
                DisbandAt = statuses[i] == "Disbanded" ? seed.AnchorUtc.AddDays(-50 + i) : null
            });
        }

        _db.RescueTeams.AddRange(seed.RescueTeams);
        await _db.SaveChangesAsync(cancellationToken);

        var memberIndex = 0;
        for (var teamIndex = 0; teamIndex < seed.RescueTeams.Count; teamIndex++)
        {
            var team = seed.RescueTeams[teamIndex];
            var count = teamIndex < 16 ? 5 : teamIndex == 16 ? 6 : 10;
            for (var i = 0; i < count; i++)
            {
                var rescuer = teamIndex < 18
                    ? deployableRescuers[memberIndex++ % deployableRescuers.Count]
                    : deployableRescuers[(teamIndex * 13 + i) % deployableRescuers.Count];
                var invitedAt = (team.CreatedAt ?? seed.StartUtc).AddHours(2 + i);
                seed.RescueTeamMembers.Add(new RescueTeamMember
                {
                    TeamId = team.Id,
                    UserId = rescuer.Id,
                    Status = "Accepted",
                    InvitedAt = invitedAt,
                    RespondedAt = invitedAt.AddMinutes(10 + i * 3),
                    IsLeader = i == 0,
                    RoleInTeam = i == 0 ? "Leader" : TeamMemberRole(i, team.TeamType),
                    CheckedIn = team.Status != "Disbanded"
                });
            }
        }

        _db.RescueTeamMembers.AddRange(seed.RescueTeamMembers);
    }

    private async Task SeedLogisticsCatalogAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var categoryDefs = new[]
        {
            ("Food",            "Thực phẩm",        "Lương thực, đồ ăn khô, thực phẩm ăn liền"),
            ("Water",           "Nước uống",        "Nước sạch, nước đóng chai, điện giải"),
            ("Medical",         "Y tế",             "Thuốc men, vật tư y tế, bộ sơ cứu"),
            ("Hygiene",         "Vệ sinh cá nhân",  "Khăn giấy, xà phòng, băng vệ sinh, tã"),
            ("Clothing",        "Quần áo",           "Quần áo sạch, áo mưa, đồ giữ ấm cơ bản"),
            ("Shelter",         "Nơi trú ẩn",        "Lều bạt, túi ngủ, vật dụng che chắn"),
            ("RepairTools",     "Công cụ sửa chữa", "Búa, đinh, cưa, dụng cụ khắc phục khẩn cấp"),
            ("RescueEquipment", "Thiết bị cứu hộ",  "Áo phao, xuồng, dây cứu sinh, bộ đàm"),
            ("Heating",         "Sưởi ấm",           "Chăn, bếp dã chiến, vật dụng giữ nhiệt"),
            ("Vehicle",         "Phương tiện",       "Xe tải, xe cứu thương, ca nô, xe địa hình"),
            ("Others",          "Khác",              "Thiết bị hỗ trợ, tín hiệu, chiếu sáng, ghi nhận hiện trường")
        };

        foreach (var (code, name, description) in categoryDefs)
        {
            seed.Categories.Add(new Category
            {
                Code = code,
                Name = name,
                Description = description,
                Quantity = 0,
                CreatedAt = seed.StartUtc,
                UpdatedAt = seed.AnchorUtc,
                CreatedBy = seed.Admins[0].Id,
                UpdatedBy = seed.Admins[0].Id
            });
        }

        _db.Categories.AddRange(seed.Categories);
        await _db.SaveChangesAsync(cancellationToken);

        var targetGroupsByName = (await _db.TargetGroups.OrderBy(t => t.Id).ToListAsync(cancellationToken))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var baseItems = BaseItemModels();
        var imageIds = ReliefItemImageIdsInSeedOrder();
        if (imageIds.Count != baseItems.Count)
        {
            throw new InvalidOperationException("Relief item image id mapping must match the seeded item model count.");
        }

        for (var i = 0; i < baseItems.Count; i++)
        {
            var template = baseItems[i];
            var category = seed.Categories.Single(c => c.Code == template.CategoryCode);
            var item = new ItemModel
            {
                CategoryId = category.Id,
                Name = template.Name,
                Description = template.Description,
                Unit = template.Unit,
                ItemType = template.ItemType,
                VolumePerUnit = template.Volume,
                WeightPerUnit = template.Weight,
                ImageUrl = GetReliefItemImageUrl(imageIds[i]) ?? $"https://cdn.resq.vn/items/{Slug(template.Name)}.jpg",
                CreatedAt = seed.StartUtc.AddDays(15 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-(i % 60)),
                UpdatedBy = seed.Managers[i % seed.Managers.Count].Id
            };

            foreach (var targetGroupName in TargetGroupNamesFor(template))
            {
                if (targetGroupsByName.TryGetValue(targetGroupName, out var targetGroup))
                {
                    item.TargetGroups.Add(targetGroup);
                }
            }

            seed.ItemModels.Add(item);
        }

        _db.ItemModels.AddRange(seed.ItemModels);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDepotsAndInventoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var depotDefs = new[]
        {
            ("Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", "46 Đống Đa, TP. Huế, Thừa Thiên Huế", 16.454572773043417, 107.56799781003454, "Available", 1_100_000m, 440_000m, 80_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg"),
            ("Ủy ban MTTQVN TP Đà Nẵng", "270 Trưng Nữ Vương, Hải Châu, Đà Nẵng", 16.080298466000496, 108.22283205420794, "Available", 1_000_000m, 480_000m, 60_000_000m, 10_000_000m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy Ban MTTQ Tỉnh Hà Tĩnh", "72 Phan Đình Phùng, TP. Hà Tĩnh, Hà Tĩnh", 18.349622333272194, 105.90102499916586, "Available", 600_000m, 260_000m, 40_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg"),
            ("Ủy ban MTTQVN Việt Nam", "46 Tràng Thi, Hoàn Kiếm, Hà Nội", 21.027819, 105.842191, "Available", 1_400_000m, 650_000m, 100_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Thăng Bình", "282 Tiểu La, thị trấn Hà Lam, huyện Thăng Bình, Quảng Nam", 15.6949, 108.4587, "Available", 250_000m, 120_000m, 12_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Quảng Ninh", "TT. Quán Hàu, huyện Quảng Ninh, Quảng Bình", 17.4619, 106.6175, "Available", 280_000m, 140_000m, 14_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Tỉnh Nghệ An", "1 Phan Đăng Lưu, TP. Vinh, Nghệ An", 18.6732581, 105.6936046, "Available", 300_000m, 150_000m, 5_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg")
        };
        var fillRatios = new[] { 0.95m, 0.70m, 0.33m, 0.95m, 0.70m, 0.33m, 0.95m };

        for (var i = 0; i < depotDefs.Length; i++)
        {
            var (name, address, lat, lon, status, capacity, weightCapacity, advanceLimit, outstandingAdvanceAmount, imageUrl) = depotDefs[i];
            var fillRatio = fillRatios[i % fillRatios.Length];
            seed.Depots.Add(new Depot
            {
                Name = name,
                Address = address,
                Location = Point(lon, lat),
                Status = status,
                Capacity = capacity,
                CurrentUtilization = decimal.Round(capacity * fillRatio, 2, MidpointRounding.AwayFromZero),
                WeightCapacity = weightCapacity,
                CurrentWeightUtilization = decimal.Round(weightCapacity * fillRatio, 2, MidpointRounding.AwayFromZero),
                AdvanceLimit = advanceLimit,
                OutstandingAdvanceAmount = outstandingAdvanceAmount,
                LastUpdatedAt = seed.AnchorUtc.AddDays(-i),
                CreatedBy = seed.Admins[0].Id,
                LastUpdatedBy = seed.Managers[i % seed.Managers.Count].Id,
                LastStatusChangedBy = seed.Managers[i % seed.Managers.Count].Id,
                ImageUrl = imageUrl
            });
        }

        _db.Depots.AddRange(seed.Depots);
        await _db.SaveChangesAsync(cancellationToken);

        var depotManagers = new List<DepotManager>();
        for (var i = 0; i < seed.Depots.Count; i++)
        {
            depotManagers.Add(new DepotManager
            {
                DepotId = seed.Depots[i].Id,
                UserId = seed.Managers[i].Id,
                AssignedAt = seed.StartUtc.AddDays(30 + i),
                AssignedBy = seed.Admins[0].Id
            });
        }
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[0].Id, UserId = seed.Managers[6].Id, AssignedAt = seed.StartUtc.AddDays(1), UnassignedAt = seed.StartUtc.AddDays(80), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[3].Id, UserId = seed.Managers[7].Id, AssignedAt = seed.StartUtc.AddDays(10), UnassignedAt = seed.StartUtc.AddDays(95), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        _db.DepotManagers.AddRange(depotManagers);

        var organizations = new List<Organization>();
        for (var i = 0; i < 14; i++)
        {
            organizations.Add(new Organization
            {
                Name = OrganizationName(i),
                Phone = Phone(7, i + 1),
                Email = $"contact{i + 1:00}@cuutro-mientrung.vn",
                IsActive = i % 11 != 0,
                CreatedAt = seed.StartUtc.AddDays(40 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i)
            });
        }
        _db.Organizations.AddRange(organizations);
        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < 90; i++)
        {
            var item = seed.ItemModels[i % seed.ItemModels.Count];
            _db.OrganizationReliefItems.Add(new OrganizationReliefItem
            {
                OrganizationId = organizations[i % organizations.Count].Id,
                ItemModelId = item.Id,
                Quantity = 80 + (i % 12) * 30,
                ReceivedDate = seed.StartUtc.AddDays(100 + i * 5),
                ExpiredDate = item.ItemType == "Consumable" ? seed.AnchorUtc.AddDays(120 + i % 120) : null,
                Notes = "Ủng hộ đợt mưa lũ miền Trung",
                ReceivedBy = seed.Managers[i % seed.Managers.Count].Id,
                CreatedAt = seed.StartUtc.AddDays(100 + i * 5)
            });
        }

        var inventoryTarget = 620;
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var itemCount = 103 + (depotIndex < 2 ? 1 : 0);
            for (var itemOffset = 0; itemOffset < itemCount && seed.Inventories.Count < inventoryTarget; itemOffset++)
            {
                var item = seed.ItemModels[(depotIndex * 7 + itemOffset) % seed.ItemModels.Count];
                var quantity = item.ItemType == "Reusable" ? 4 + itemOffset % 14 : 160 + (itemOffset % 30) * 20;
                var missionReserved = itemOffset % 9 == 0 ? Math.Min(quantity / 6, 40) : 0;
                var transferReserved = itemOffset % 13 == 0 ? Math.Min(quantity / 10, 25) : 0;
                seed.Inventories.Add(new SupplyInventory
                {
                    DepotId = seed.Depots[depotIndex].Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    MissionReservedQuantity = missionReserved,
                    TransferReservedQuantity = transferReserved,
                    LastStockedAt = seed.AnchorUtc.AddDays(-itemOffset % 90),
                    IsDeleted = false
                });
            }
        }

        var lifeJacketModel = seed.ItemModels.Single(m => m.Name == "Áo phao cứu sinh");
        var blanketModel = seed.ItemModels.Single(m => m.Name == "Chăn ấm giữ nhiệt");
        EnsureEssentialDepotStock(seed, lifeJacketModel, blanketModel);

        _db.SupplyInventories.AddRange(seed.Inventories);
        await _db.SaveChangesAsync(cancellationToken);

        var consumableInventories = seed.Inventories
            .Where(i => seed.ItemModels.First(m => m.Id == i.ItemModelId).ItemType == "Consumable")
            .Take(395)
            .ToList();
        foreach (var inventory in consumableInventories)
        {
            var received = seed.AnchorUtc.AddDays(-30 - seed.Lots.Count % 300);
            var quantity = Math.Max(20, (inventory.Quantity ?? 100) / 2);
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = quantity,
                RemainingQuantity = Math.Max(0, quantity - inventory.MissionReservedQuantity - inventory.TransferReservedQuantity),
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(6 + seed.Lots.Count % 18),
                SourceType = seed.Lots.Count % 3 == 0 ? "Purchase" : "Donation",
                SourceId = seed.Lots.Count + 1,
                CreatedAt = received
            });
        }
        EnsureEssentialBlanketLots(seed, blanketModel);
        _db.SupplyInventoryLots.AddRange(seed.Lots);

        var reusableModels = seed.ItemModels.Where(m => m.ItemType == "Reusable").ToList();
        for (var i = 0; i < 220; i++)
        {
            var item = reusableModels[i % reusableModels.Count];
            var depot = seed.Depots[i % seed.Depots.Count];
            seed.ReusableItems.Add(new ReusableItem
            {
                DepotId = depot.Id,
                ItemModelId = item.Id,
                SerialNumber = $"{Slug(item.Name ?? "item").ToUpperInvariant()}-{Area(i).Code}-{i + 1:00000}",
                Status = i % 17 == 0 ? "Maintenance" : i % 13 == 0 ? "Reserved" : "Available",
                Condition = i % 11 == 0 ? "Fair" : i % 29 == 0 ? "Poor" : "Good",
                Note = i % 17 == 0 ? "Đang kiểm tra sau nhiệm vụ" : null,
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i % 90),
                IsDeleted = false
            });
        }
        EnsureLifeJacketReusableUnits(seed, lifeJacketModel);
        _db.ReusableItems.AddRange(seed.ReusableItems);

        await SeedVatInvoicesAsync(seed);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedVatInvoicesAsync(DemoSeedContext seed)
    {
        var invoices = new List<VatInvoice>();
        for (var i = 0; i < 50; i++)
        {
            var date = DateOnly.FromDateTime(seed.StartUtc.AddDays(180 + i * 17));
            invoices.Add(new VatInvoice
            {
                InvoiceSerial = $"AA/{date.Year % 100:00}E",
                InvoiceNumber = $"{1800 + i:0000000}",
                SupplierName = SupplierName(i),
                SupplierTaxCode = $"330{1234560 + i}",
                InvoiceDate = date,
                TotalAmount = 8_500_000 + i * 420_000,
                FileUrl = $"https://cdn.resq.vn/vat/{date.Year}-{i + 1:000}.pdf",
                CreatedAt = VnToUtc(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))))
            });
        }

        _db.VatInvoices.AddRange(invoices);
        await _db.SaveChangesAsync();

        foreach (var invoice in invoices)
        {
            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(invoice.Id * 5 + j) % seed.ItemModels.Count];
                var quantity = 20 + (invoice.Id + j) % 80;
                var price = item.ItemType == "Reusable" ? 450_000 + j * 250_000 : 12_000 + j * 8_000;
                _db.VatInvoiceItems.Add(new VatInvoiceItem
                {
                    VatInvoiceId = invoice.Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    UnitPrice = price,
                    CreatedAt = invoice.CreatedAt
                });
            }
        }
    }

    private async Task SeedEmergencyAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var clusterSosCounts = Enumerable.Range(0, 110).Select(i => i < 20 ? 4 : 3).ToArray();
        var createdSos = new List<SosRequest>();

        for (var i = 0; i < clusterSosCounts.Length; i++)
        {
            var area = Area(i);
            var localDate = RandomEventLocal(seed, i);
            var severity = i < 22 ? "Critical" : i < 80 ? "High" : i < 101 ? "Medium" : "Low";
            var offsets = new List<(double Lat, double Lon)>();
            for (var j = 0; j < clusterSosCounts[i]; j++)
            {
                var dLat = ((j % 2 == 0 ? 1 : -1) * (0.004 + (j + i % 3) * 0.001));
                var dLon = ((j % 3 == 0 ? 1 : -1) * (0.005 + (j + i % 4) * 0.001));
                offsets.Add((area.Lat + dLat, area.Lon + dLon));
            }

            var avgLat = offsets.Average(p => p.Lat);
            var avgLon = offsets.Average(p => p.Lon);
            var radius = Math.Max(0.8, offsets.Max(p => DistanceKm(avgLat, avgLon, p.Lat, p.Lon)) + 0.5);
            var cluster = new SosCluster
            {
                CenterLocation = Point(avgLon, avgLat),
                RadiusKm = Math.Round(radius, 2),
                SeverityLevel = severity,
                WaterLevel = severity == "Critical" ? "Ngập 1.5-2m, nước đang lên" : severity == "High" ? "Ngập 0.8-1.2m" : "Ngập cục bộ",
                VictimEstimated = 10 + clusterSosCounts[i] * (3 + i % 8),
                ChildrenCount = 1 + i % 9,
                ElderlyCount = i % 7,
                MedicalUrgencyScore = Math.Round(0.35 + (i % 60) / 100.0, 2),
                CreatedAt = VnToUtc(localDate),
                LastUpdatedAt = VnToUtc(localDate.AddHours(3)),
                Status = i < 100 ? "InProgress" : "Pending"
            };
            seed.SosClusters.Add(cluster);

            for (var j = 0; j < clusterSosCounts[i]; j++)
            {
                var victim = seed.Victims[(i * 4 + j) % seed.Victims.Count];
                var situation = Situation(i + j);
                var priority = severity;
                var createdAt = VnToUtc(localDate.AddMinutes(j * 18));
                var onBehalf = (i + j) % 5 == 0;
                var reporterOther = !onBehalf && (i + j) % 10 == 0;
                var reporter = reporterOther ? seed.Victims[(i * 7 + j + 11) % seed.Victims.Count] : victim;
                var coordinator = seed.Coordinators[(i + j) % seed.Coordinators.Count];
                var status = i < 12 ? "Pending" : i < 70 ? "Resolved" : i < 95 ? "InProgress" : i < 104 ? "Assigned" : "Cancelled";
                var people = 1 + (i + j) % 6;
                var hasInjured = situation is "Medical" or "Landslide" || (i + j) % 11 == 0;

                createdSos.Add(new SosRequest
                {
                    PacketId = StableGuid($"packet-{i}-{j}"),
                    Cluster = cluster,
                    UserId = victim.Id,
                    Location = Point(offsets[j].Lon, offsets[j].Lat),
                    LocationAccuracy = 12 + (i + j) % 35,
                    SosType = situation is "NeedSupplies" ? "Relief" : hasInjured || situation is "Flooding" ? "Both" : "Rescue",
                    RawMessage = SosMessage(situation, people, hasInjured),
                    StructuredData = Json(new
                    {
                        incident = new { situation, water_level = cluster.WaterLevel },
                        people_count = new { adult = Math.Max(1, people - 2), child = (i + j) % 3, elderly = (i + j) % 2, pregnant = (i + j) % 17 == 0 ? 1 : 0 },
                        has_injured = hasInjured,
                        can_move = situation is not "CannotMove" and not "Stranded",
                        medical_issues = hasInjured ? new[] { "chấn thương nhẹ", "hạ thân nhiệt" } : Array.Empty<string>(),
                        supplies = SuppliesFor(situation),
                        address = $"{12 + i % 80} {area.Address}",
                        vulnerable = new { elderly = (i + j) % 2, children = (i + j) % 3 }
                    }),
                    NetworkMetadata = Json(new { source = onBehalf ? "hotline" : "mobile", network = (i + j) % 8 == 0 ? "mesh" : "4g", battery = 20 + (i + j) % 80 }),
                    SenderInfo = Json(new { user_id = victim.Id, phone = victim.Phone }),
                    VictimInfo = Json(new { user_name = FullName(victim), user_phone = victim.Phone }),
                    ReporterInfo = Json(new { user_name = onBehalf ? FullName(coordinator) : FullName(reporter), user_phone = onBehalf ? coordinator.Phone : reporter.Phone, is_online = !onBehalf }),
                    IsSentOnBehalf = onBehalf,
                    OriginId = onBehalf ? $"hotline-{createdAt:yyyyMMddHHmm}-{i:000}" : null,
                    PriorityLevel = priority,
                    PriorityScore = PriorityScore(priority, i + j),
                    Status = status,
                    AiAnalysis = null,
                    ReceivedAt = createdAt.AddMinutes(onBehalf ? 2 : 0),
                    Timestamp = new DateTimeOffset(createdAt).ToUnixTimeMilliseconds(),
                    CreatedAt = createdAt,
                    LastUpdatedAt = createdAt.AddHours(status == "Resolved" ? 8 : 1),
                    ReviewedAt = status == "Pending" ? null : createdAt.AddMinutes(20 + i % 30),
                    ReviewedById = status == "Pending" ? null : coordinator.Id,
                    CreatedByCoordinatorId = onBehalf ? coordinator.Id : null
                });
            }
        }

        var hueStadium = GetHueStadiumAssemblyPoint(seed);
        if (hueStadium?.Location is not null)
        {
            var stadiumLat = hueStadium.Location.Y;
            var stadiumLon = hueStadium.Location.X;
            var offsets = new[]
            {
                (0.0068, -0.0045),
                (-0.0059, 0.0062),
                (0.0041, 0.0086),
                (-0.0076, -0.0033),
                (0.0091, 0.0019),
                (-0.0038, -0.0087),
                (0.0024, -0.0101),
                (-0.0094, 0.0041),
                (0.0107, -0.0018),
                (-0.0063, 0.0094)
            };
            var nearbyAddresses = new[]
            {
                "12 Hà Huy Tập, Phú Nhuận, Huế",
                "37 Nguyễn Huệ, Phú Nhuận, Huế",
                "18 Lê Quý Đôn, Vĩnh Ninh, Huế",
                "54 Nguyễn Trường Tộ, Phước Vĩnh, Huế",
                "29 Đống Đa, Phú Nhuận, Huế",
                "8 Nguyễn Công Trứ, Phú Hội, Huế",
                "41 Trần Cao Vân, Vĩnh Ninh, Huế",
                "66 Bà Triệu, Xuân Phú, Huế",
                "23 Hoàng Hoa Thám, Phú Nhuận, Huế",
                "15 Phan Bội Châu, Vĩnh Ninh, Huế"
            };

            for (var i = 0; i < HueStadiumUnclusteredSosCount; i++)
            {
                var victim = seed.Victims[110 + i];
                var coordinator = seed.Coordinators[i % seed.Coordinators.Count];
                var situation = (i % 4) switch
                {
                    0 => "Stranded",
                    1 => "Flooding",
                    2 => "CannotMove",
                    _ => "NeedSupplies"
                };
                var status = i < 4 ? "Pending" : i < 7 ? "Assigned" : i < 9 ? "InProgress" : "Resolved";
                var people = 2 + i % 4;
                var hasInjured = i % 3 == 0;
                var localDate = new DateTime(2026, 4, 6 + i, 6 + i % 5, 15 + i * 3 % 35, 0, DateTimeKind.Unspecified);
                var createdAt = VnToUtc(localDate);
                var location = Point(stadiumLon + offsets[i].Item2, stadiumLat + offsets[i].Item1);

                createdSos.Add(new SosRequest
                {
                    PacketId = StableGuid($"packet-hue-stadium-scatter-{i}"),
                    ClusterId = null,
                    UserId = victim.Id,
                    Location = location,
                    LocationAccuracy = 9 + i,
                    SosType = situation is "NeedSupplies" ? "Relief" : situation is "Flooding" ? "Both" : "Rescue",
                    RawMessage = SosMessage(situation, people, hasInjured),
                    StructuredData = Json(new
                    {
                        incident = new { situation, water_level = i % 2 == 0 ? "Ngập cục bộ quanh sân vận động" : "Ngập sâu ở kiệt nhỏ quanh khu dân cư" },
                        people_count = new { adult = Math.Max(1, people - 1), child = i % 2, elderly = i % 3 == 0 ? 1 : 0, pregnant = i == 7 ? 1 : 0 },
                        has_injured = hasInjured,
                        can_move = situation is not "CannotMove" and not "Stranded",
                        medical_issues = hasInjured ? new[] { "trầy xước", "mệt do ngâm nước lâu" } : Array.Empty<string>(),
                        supplies = SuppliesFor(situation),
                        address = nearbyAddresses[i],
                        assembly_point_reference = new { assembly_point_code = hueStadium.Code, assembly_point_name = hueStadium.Name }
                    }),
                    NetworkMetadata = Json(new { source = "mobile", network = i % 4 == 0 ? "4g" : "wifi", battery = 36 + i * 5 }),
                    SenderInfo = Json(new { user_id = victim.Id, phone = victim.Phone }),
                    VictimInfo = Json(new { user_name = FullName(victim), user_phone = victim.Phone }),
                    ReporterInfo = Json(new { user_name = FullName(victim), user_phone = victim.Phone, is_online = true }),
                    IsSentOnBehalf = false,
                    OriginId = $"mobile-hue-stadium-{i + 1:000}",
                    PriorityLevel = i < 3 ? "High" : i < 8 ? "Medium" : "Low",
                    PriorityScore = i < 3 ? 69 + i : i < 8 ? 48 + i : 28 + i,
                    Status = status,
                    AiAnalysis = null,
                    ReceivedAt = createdAt,
                    Timestamp = new DateTimeOffset(createdAt).ToUnixTimeMilliseconds(),
                    CreatedAt = createdAt,
                    LastUpdatedAt = createdAt.AddHours(status == "Resolved" ? 6 : 2),
                    ReviewedAt = status == "Pending" ? null : createdAt.AddMinutes(16 + i),
                    ReviewedById = status == "Pending" ? null : coordinator.Id,
                    CreatedByCoordinatorId = null
                });
            }
        }

        _db.SosClusters.AddRange(seed.SosClusters);
        _db.SosRequests.AddRange(createdSos);
        await _db.SaveChangesAsync(cancellationToken);
        seed.SosRequests.AddRange(createdSos);

        var companions = new List<SosRequestCompanion>();
        for (var i = 0; i < 130; i++)
        {
            var sos = seed.SosRequests[(i * 3) % seed.SosRequests.Count];
            var companion = seed.Victims[(i * 7 + 17) % seed.Victims.Count];
            if (companion.Id == sos.UserId)
            {
                companion = seed.Victims[(i * 7 + 18) % seed.Victims.Count];
            }

            companions.Add(new SosRequestCompanion
            {
                SosRequestId = sos.Id,
                UserId = companion.Id,
                PhoneNumber = companion.Phone,
                AddedAt = (sos.CreatedAt ?? seed.StartUtc).AddMinutes(4)
            });
        }
        _db.SosRequestCompanions.AddRange(companions.GroupBy(c => new { c.SosRequestId, c.UserId }).Select(g => g.First()));

        foreach (var sos in seed.SosRequests)
        {
            var createdAt = sos.CreatedAt ?? seed.StartUtc;
            _db.SosRuleEvaluations.Add(new SosRuleEvaluation
            {
                SosRequestId = sos.Id,
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                MedicalScore = sos.PriorityLevel is "Critical" ? 9 : sos.PriorityLevel is "High" ? 7 : 4,
                FoodScore = (sos.Id % 5) + 2,
                InjuryScore = sos.RawMessage?.Contains("bị thương", StringComparison.OrdinalIgnoreCase) == true ? 8 : 1,
                MobilityScore = sos.RawMessage?.Contains("không thể di chuyển", StringComparison.OrdinalIgnoreCase) == true ? 9 : 4,
                EnvironmentScore = sos.PriorityLevel is "Critical" ? 9 : 5,
                TotalScore = sos.PriorityScore,
                PriorityLevel = sos.PriorityLevel,
                RuleVersion = "v1.0",
                ItemsNeeded = Json(new[] { "Water", "Food", "Medicine" }),
                BreakdownJson = Json(new { priority = sos.PriorityLevel, reason = "Generated by deterministic demo seed" }),
                DetailsJson = sos.StructuredData,
                CreatedAt = createdAt.AddMinutes(1)
            });

            for (var u = 0; u < 2; u++)
            {
                _db.SosRequestUpdates.Add(new SosRequestUpdate
                {
                    SosRequestId = sos.Id,
                    Type = u == 0 ? "CoordinatorNote" : sos.Status == "Resolved" ? "Rescued" : "TeamApproaching",
                    Content = u == 0 ? "Đã tiếp nhận thông tin và kiểm tra vị trí." : SosUpdateContent(sos.Status),
                    CreatedAt = createdAt.AddMinutes(15 + u * 35),
                    Status = "Visible"
                });
            }
        }

        foreach (var sos in seed.SosRequests.Take(255))
        {
            _db.SosAiAnalyses.Add(new SosAiAnalysis
            {
                SosRequestId = sos.Id,
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SosAssessment",
                SuggestedSeverityLevel = sos.PriorityLevel,
                SuggestedPriority = sos.PriorityLevel,
                Explanation = $"Đề xuất {sos.PriorityLevel} dựa trên vị trí, khả năng di chuyển và nhóm dễ tổn thương.",
                ConfidenceScore = 0.72 + (sos.Id % 24) / 100.0,
                SuggestionScope = "DemoSeed",
                Metadata = Json(new { risk_factors = new[] { "flood", "vulnerable_people", "limited_access" } }),
                CreatedAt = (sos.CreatedAt ?? seed.StartUtc).AddMinutes(2),
                AdoptedAt = sos.Status == "Pending" ? null : (sos.ReviewedAt ?? sos.CreatedAt)?.AddMinutes(1)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMissionsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var missionClusters = seed.SosClusters.Take(100).ToList();
        for (var i = 0; i < missionClusters.Count; i++)
        {
            var cluster = missionClusters[i];
            var createdAt = (cluster.CreatedAt ?? seed.StartUtc).AddMinutes(18);
            var status = i < 20 ? "Planned" : i < 45 ? "OnGoing" : i < 95 ? "Completed" : "Incompleted";
            seed.Missions.Add(new Mission
            {
                ClusterId = cluster.Id,
                MissionType = MissionType(i, cluster.SeverityLevel),
                PriorityScore = PriorityScore(cluster.SeverityLevel ?? "Medium", i),
                Status = status,
                StartTime = status == "Planned" ? null : createdAt.AddMinutes(15),
                ExpectedEndTime = createdAt.AddHours(5 + i % 5),
                IsCompleted = status == "Completed",
                CreatedById = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = createdAt,
                CompletedAt = status == "Completed" ? createdAt.AddHours(5 + i % 5) : null
            });
        }

        _db.Missions.AddRange(seed.Missions);
        await _db.SaveChangesAsync(cancellationToken);

        var supplyItems = seed.ItemModels.Where(m => m.ItemType == "Consumable").Take(45).ToList();
        foreach (var mission in seed.Missions)
        {
            for (var j = 0; j < 2; j++)
            {
                var item = supplyItems[(mission.Id + j * 7) % supplyItems.Count];
                var inventory = seed.Inventories.First(i => i.ItemModelId == item.Id);
                _db.MissionItems.Add(new MissionItem
                {
                    MissionId = mission.Id,
                    ItemModelId = item.Id,
                    RequiredQuantity = 60 + (mission.Id + j) % 180,
                    AllocatedQuantity = 50 + (mission.Id + j) % 150,
                    SourceDepotId = inventory.DepotId,
                    BufferRatio = 0.10 + (j * 0.05)
                });
            }
        }

        for (var i = 0; i < seed.Missions.Count; i++)
        {
            var teamsForMission = i < 40 ? 2 : 1;
            for (var j = 0; j < teamsForMission; j++)
            {
                var team = TeamForMission(seed, i, j);
                var cluster = seed.SosClusters.First(c => c.Id == seed.Missions[i].ClusterId);
                var status = seed.Missions[i].Status switch
                {
                    "Completed" => i % 3 == 0 ? "Reported" : "CompletedWaitingReport",
                    "OnGoing" => "InProgress",
                    "Incompleted" => "Cancelled",
                    _ => "Assigned"
                };
                seed.MissionTeams.Add(new MissionTeam
                {
                    MissionId = seed.Missions[i].Id,
                    RescuerTeamId = team.Id,
                    TeamType = team.TeamType,
                    CurrentLocation = OffsetPoint(cluster.CenterLocation, 0.004 * (j + 1), -0.003 * (j + 1)),
                    LocationUpdatedAt = (seed.Missions[i].StartTime ?? seed.Missions[i].CreatedAt)?.AddMinutes(50),
                    LocationSource = "GPS",
                    Status = status,
                    AssignedAt = seed.Missions[i].CreatedAt?.AddMinutes(10 + j * 8),
                    UnassignedAt = status == "Cancelled" ? seed.Missions[i].CreatedAt?.AddHours(2) : null,
                    Note = "Giao đội theo năng lực và khoảng cách demo",
                    CreatedAt = seed.Missions[i].CreatedAt
                });
            }
        }

        _db.MissionTeams.AddRange(seed.MissionTeams);
        await _db.SaveChangesAsync(cancellationToken);

        SyncRescueTeamStatusesFromAssignments(seed);

        foreach (var missionTeam in seed.MissionTeams)
        {
            var sourceMembers = seed.RescueTeamMembers.Where(m => m.TeamId == missionTeam.RescuerTeamId).Take(5).ToList();
            foreach (var member in sourceMembers)
            {
                _db.MissionTeamMembers.Add(new MissionTeamMember
                {
                    MissionTeamId = missionTeam.Id,
                    RescuerId = member.UserId,
                    RoleInTeam = member.RoleInTeam,
                    JoinedAt = missionTeam.AssignedAt?.AddMinutes(5),
                    LeftAt = missionTeam.Status is "Reported" or "CompletedWaitingReport" ? missionTeam.AssignedAt?.AddHours(7) : null
                });
            }
        }

        foreach (var mission in seed.Missions)
        {
            var missionTeams = seed.MissionTeams.Where(t => t.MissionId == mission.Id).ToList();
            var activities = 4 + (seed.Missions.IndexOf(mission) < 20 ? 1 : 0);
            var clusterSos = seed.SosRequests.Where(s => s.ClusterId == mission.ClusterId).ToList();
            for (var step = 1; step <= activities; step++)
            {
                var team = missionTeams[(step - 1) % missionTeams.Count];
                var sos = clusterSos[(step - 1) % clusterSos.Count];
                var type = ActivityType(step, activities, mission.MissionType);
                var hasDepot = type is "COLLECT_SUPPLIES" or "DELIVER_SUPPLIES" or "RETURN_SUPPLIES";
                var depot = hasDepot
                    ? OperationalDepotForActivity(seed, mission.Id, step)
                    : seed.Depots[(mission.Id + step) % seed.Depots.Count];
                var activityStatus = ActivityStatusFor(mission.Status, step, activities);
                var assigned = (mission.StartTime ?? mission.CreatedAt)?.AddMinutes(step * 35);
                seed.MissionActivities.Add(new MissionActivity
                {
                    MissionId = mission.Id,
                    Step = step,
                    ActivityType = type,
                    Description = ActivityDescription(type, depot.Name, sos.RawMessage),
                    Target = Json(new { address = JsonDocument.Parse(sos.StructuredData ?? "{}").RootElement.TryGetProperty("address", out var address) ? address.GetString() : "Khu dân cư", sos_request_id = sos.Id }),
                    Items = hasDepot ? Json(new[] { new { item_id = seed.ItemModels[(mission.Id + step) % seed.ItemModels.Count].Id, quantity = 20 + step * 10, unit = "đơn vị" } }) : null,
                    TargetLocation = hasDepot ? depot.Location : sos.Location,
                    Status = activityStatus,
                    AssignedAt = assigned,
                    CompletedAt = activityStatus is "Succeed" ? assigned?.AddMinutes(40 + step * 10) : null,
                    LastDecisionBy = seed.Coordinators[mission.Id % seed.Coordinators.Count].Id,
                    MissionTeamId = team.Id,
                    Priority = mission.PriorityScore >= 80 ? "Critical" : mission.PriorityScore >= 60 ? "High" : "Medium",
                    EstimatedTime = 35 + step * 15,
                    SosRequestId = sos.Id,
                    DepotId = hasDepot ? depot.Id : null,
                    DepotName = hasDepot ? depot.Name : null,
                    DepotAddress = hasDepot ? depot.Address : null,
                    AssemblyPointId = seed.RescueTeams.First(t => t.Id == team.RescuerTeamId).AssemblyPointId
                });
            }
        }

        _db.MissionActivities.AddRange(seed.MissionActivities);
        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < 35; i++)
        {
            var team = seed.MissionTeams.Where(t => t.Status is "Assigned" or "InProgress").ElementAt(i % seed.MissionTeams.Count(t => t.Status is "Assigned" or "InProgress"));
            var activity = seed.MissionActivities.First(a => a.MissionTeamId == team.Id);
            var support = i % 4 == 0 ? seed.SosRequests[(i * 9) % seed.SosRequests.Count] : null;
            var incident = new TeamIncident
            {
                MissionTeamId = team.Id,
                MissionActivityId = activity.Id,
                Location = OffsetPoint(activity.TargetLocation, 0.001 * (i % 3), -0.001 * (i % 2)),
                Description = IncidentDescription(i),
                Status = i % 3 == 0 ? "Resolved" : i % 3 == 1 ? "InProgress" : "Reported",
                IncidentScope = i % 2 == 0 ? "Activity" : "Mission",
                IncidentType = IncidentType(i),
                DecisionCode = i % 3 == 0 ? "COORDINATOR_REVIEWED" : null,
                DetailJson = Json(new { severity = i % 5 == 0 ? "High" : "Medium", weather = "mưa lớn", road = "ngập sâu" }),
                PayloadVersion = 1,
                NeedSupportSos = support is not null,
                NeedReassignActivity = i % 6 == 0,
                SupportSosRequestId = support?.Id,
                ReportedBy = seed.RescueTeamMembers.First(m => m.TeamId == team.RescuerTeamId).UserId,
                ReportedAt = activity.AssignedAt?.AddMinutes(45 + i)
            };
            _db.TeamIncidents.Add(incident);
            await _db.SaveChangesAsync(cancellationToken);
            _db.TeamIncidentActivities.Add(new TeamIncidentActivity
            {
                TeamIncidentId = incident.Id,
                MissionActivityId = activity.Id,
                OrderIndex = 1,
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedChatAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var sosByVictim = seed.SosRequests
            .Where(s => s.UserId.HasValue)
            .GroupBy(s => s.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

        for (var i = 0; i < 140; i++)
        {
            var victim = seed.Victims[i];
            var sos = sosByVictim.GetValueOrDefault(victim.Id) ?? seed.SosRequests[i % seed.SosRequests.Count];
            var mission = seed.Missions.FirstOrDefault(m => m.ClusterId == sos.ClusterId);
            var status = i < 20 ? "AiAssist" : i < 50 ? "WaitingCoordinator" : i < 95 ? "CoordinatorActive" : "Closed";
            seed.Conversations.Add(new Conversation
            {
                VictimId = victim.Id,
                MissionId = i % 3 == 0 ? mission?.Id : null,
                Status = status,
                SelectedTopic = status == "AiAssist" ? "SosRequestSupport" : "Cần cập nhật ETA và vật phẩm",
                LinkedSosRequestId = sos.Id,
                CreatedAt = (sos.CreatedAt ?? seed.StartUtc).AddMinutes(8),
                UpdatedAt = (sos.CreatedAt ?? seed.StartUtc).AddHours(status == "Closed" ? 9 : 1)
            });
        }

        _db.Conversations.AddRange(seed.Conversations);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var conversation in seed.Conversations)
        {
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversation.Id % seed.Coordinators.Count];
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = victim.Id,
                RoleInConversation = "Victim",
                JoinedAt = conversation.CreatedAt,
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = coordinator.Id,
                RoleInConversation = "Coordinator",
                JoinedAt = conversation.CreatedAt?.AddMinutes(conversation.Status == "WaitingCoordinator" ? 30 : 3),
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
        }

        var messages = new List<Message>();
        for (var conversationIndex = 0; conversationIndex < seed.Conversations.Count; conversationIndex++)
        {
            var conversation = seed.Conversations[conversationIndex];
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversationIndex % seed.Coordinators.Count];
            var count = 13 + (conversationIndex < 80 ? 1 : 0);
            for (var i = 0; i < count; i++)
            {
                var messageType = i == 1 ? "AiMessage" : i % 7 == 0 ? "SystemMessage" : "UserMessage";
                messages.Add(new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = messageType == "SystemMessage" ? null : messageType == "AiMessage" ? null : i % 2 == 0 ? victim.Id : coordinator.Id,
                    Content = ChatMessage(i, conversation.Status),
                    MessageType = messageType,
                    CreatedAt = (conversation.CreatedAt ?? seed.StartUtc).AddMinutes(i * 4)
                });
            }
        }

        _db.Messages.AddRange(messages);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSupplyRequestsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var inProgressStatuses = new[]
        {
            ("Pending", "WaitingForApproval"),
            ("Accepted", "Approved"),
            ("Preparing", "Approved"),
            ("Shipping", "InTransit")
        };
        const int depotOneTwoRequestCount = 24;
        const int depotOneTwoIncompleteRequestCount = 12;
        var completedStatus = ("Completed", "Received");
        var completedOnlyDepots = seed.Depots
            .Skip(2)
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        for (var i = 0; i < 95; i++)
        {
            Depot requesting;
            Depot source;
            (string SourceStatus, string RequestingStatus) status;

            if (i < depotOneTwoRequestCount)
            {
                requesting = seed.Depots[i % 2];
                source = seed.Depots[(i + 1) % 2];
                status = i < depotOneTwoIncompleteRequestCount
                    ? inProgressStatuses[i % inProgressStatuses.Length]
                    : completedStatus;
            }
            else
            {
                var completedIndex = i - depotOneTwoRequestCount;
                requesting = completedOnlyDepots[completedIndex % completedOnlyDepots.Count];
                source = completedOnlyDepots[(completedIndex + 2) % completedOnlyDepots.Count];
                status = completedStatus;
            }

            var created = RandomEventUtc(seed, i + 220);
            var sourceManager = seed.Managers[(source.Id - 1) % seed.Managers.Count];
            var requestingManager = seed.Managers[(requesting.Id - 1) % seed.Managers.Count];
            seed.SupplyRequests.Add(new DepotSupplyRequest
            {
                RequestingDepotId = requesting.Id,
                SourceDepotId = source.Id,
                Note = SupplyRequestNote(i),
                PriorityLevel = status.SourceStatus == "Pending"
                    ? "Urgent"
                    : status.SourceStatus is "Accepted" or "Preparing" or "Shipping"
                        ? "High"
                        : i % 5 == 0 ? "High" : "Medium",
                SourceStatus = status.SourceStatus,
                RequestingStatus = status.RequestingStatus,
                RejectedReason = null,
                RequestedBy = requestingManager.Id,
                CreatedAt = created,
                AutoRejectAt = status.SourceStatus == "Pending" ? created.AddHours(i % 3 == 0 ? 2 : 6) : null,
                HighEscalationNotified = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending",
                HighEscalationNotifiedAt = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending"
                    ? created.AddMinutes(60)
                    : null,
                UrgentEscalationNotified = status.SourceStatus == "Pending",
                UrgentEscalationNotifiedAt = status.SourceStatus == "Pending" ? created.AddMinutes(25) : null,
                RespondedAt = status.SourceStatus == "Pending" ? null : created.AddMinutes(30),
                ShippedAt = status.SourceStatus is "Shipping" or "Completed" ? created.AddHours(3) : null,
                CompletedAt = status.SourceStatus == "Completed" ? created.AddHours(7) : null,
                UpdatedAt = status.SourceStatus == "Completed"
                    ? created.AddHours(7)
                    : status.SourceStatus == "Shipping"
                        ? created.AddHours(3)
                        : created.AddHours(1),
                AcceptedBy = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                RejectedBy = null,
                PreparedBy = status.SourceStatus is "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                ShippedBy = status.SourceStatus is "Shipping" or "Completed" ? sourceManager.Id : null,
                CompletedBy = status.SourceStatus == "Completed" ? sourceManager.Id : null,
                ConfirmedBy = status.SourceStatus == "Completed" ? requestingManager.Id : null
            });
        }

        _db.DepotSupplyRequests.AddRange(seed.SupplyRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.SupplyRequests)
        {
            var itemCount = request.Id % 2 == 0 ? 3 : 2;
            for (var j = 0; j < itemCount; j++)
            {
                var item = seed.ItemModels[(request.Id * 3 + j) % seed.ItemModels.Count];
                _db.DepotSupplyRequestItems.Add(new DepotSupplyRequestItem
                {
                    DepotSupplyRequestId = request.Id,
                    ItemModelId = item.Id,
                    Quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40 + request.Id % 30
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedFinanceAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var systemFund = new SystemFund
        {
            Name = "Quỹ điều phối hệ thống",
            Balance = 4_500_000_000m,
            LastUpdatedAt = seed.AnchorUtc
        };
        _db.SystemFunds.Add(systemFund);

        var campaignPlans = new List<(FundCampaign Campaign, decimal PlannedRaised)>();
        var donationRatios = new[] { 22m, 18m, 15m, 13m, 11m, 9m, 7m, 5m };

        for (var i = 0; i < 11; i++)
        {
            var start = DateOnly.FromDateTime(seed.StartUtc.AddDays(120 + i * 75));
            var campaign = new FundCampaign
            {
                Code = $"FC-{start.Year}-B{i + 1:00}",
                Name = CampaignName(i),
                Region = "Huế - Đà Nẵng - Quảng Trị - Quảng Nam - Quảng Ngãi",
                CampaignStartDate = start,
                CampaignEndDate = start.AddDays(45),
                TargetAmount = 1_500_000_000m + i * 150_000_000m,
                // Calculated from seeded donation/disbursement history below.
                TotalAmount = 0m,
                CurrentBalance = 0m,
                Status = i % 6 == 0 ? "Closed" : "Active",
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(start.ToDateTime(TimeOnly.MinValue)),
                LastModifiedBy = seed.Admins[0].Id,
                LastModifiedAt = seed.AnchorUtc.AddDays(-i),
                IsDeleted = false
            };

            seed.FundCampaigns.Add(campaign);
            campaignPlans.Add((campaign, 450_000_000m + i * 155_000_000m));
        }
        _db.FundCampaigns.AddRange(seed.FundCampaigns);
        await _db.SaveChangesAsync(cancellationToken);

        var donations = new List<Donation>();
        for (var campaignIndex = 0; campaignIndex < campaignPlans.Count; campaignIndex++)
        {
            var (campaign, plannedRaised) = campaignPlans[campaignIndex];
            var remaining = plannedRaised;
            var campaignStartLocal = campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(8, 0));

            for (var donationIndex = 0; donationIndex < donationRatios.Length; donationIndex++)
            {
                var amount = donationIndex == donationRatios.Length - 1
                    ? remaining
                    : decimal.Round(plannedRaised * donationRatios[donationIndex] / 100m, 0, MidpointRounding.AwayFromZero);
                remaining -= amount;

                var donorSeed = campaignIndex * 17 + donationIndex;
                var (last, first) = VietnameseName(donorSeed);
                var donorName = donationIndex % 3 == 0
                    ? OrganizationName(donorSeed)
                    : $"{last} {first}";

                var orderId = $"{campaign.CampaignStartDate:yyMMdd}{campaign.Id:00}{donationIndex + 1:0000}";
                var paidAtLocal = campaignStartLocal
                    .AddDays(Math.Min(40, donationIndex * 5 + campaignIndex % 3))
                    .AddHours(donationIndex % 5);
                var paidAtUtc = VnToUtc(paidAtLocal);

                donations.Add(new Donation
                {
                    FundCampaignId = campaign.Id,
                    DonorName = donorName,
                    DonorEmail = $"donor-c{campaign.Id:00}-{donationIndex + 1:000}@resq.vn",
                    Amount = amount,
                    OrderId = orderId,
                    TransactionId = $"DEMO-TRX-{campaign.Id:00}-{donationIndex + 1:0000}",
                    Status = Status.Succeed.ToString(),
                    PaymentMethodCode = donationIndex % 2 == 0 ? PaymentMethodCode.PAYOS : PaymentMethodCode.MOMO,
                    PaidAt = paidAtUtc,
                    Note = "Đóng góp ủng hộ chiến dịch miền Trung.",
                    PaymentAuditInfo = donationIndex % 2 == 0
                        ? $"[PAYOS:order={orderId}]"
                        : $"[MOMO:campaign={campaign.Id},seq={donationIndex + 1}]",
                    IsPrivate = donationIndex % 4 == 1,
                    CreatedAt = paidAtUtc.AddMinutes(-10)
                });
            }
        }

        _db.Donations.AddRange(donations);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(donations.Select(donation => new FundTransaction
        {
            FundCampaignId = donation.FundCampaignId,
            Type = TransactionType.Donation.ToString(),
            Direction = "in",
            Amount = donation.Amount,
            ReferenceType = TransactionReferenceType.Donation.ToString(),
            ReferenceId = donation.Id,
            CreatedBy = null,
            CreatedAt = donation.PaidAt ?? donation.CreatedAt
        }));

        var depotFundCounts = new[] { 3, 2, 1, 3, 2, 1, 1 };
        var depotFundBalanceRatios = new[]
        {
            new[] { 0.50m, 0.30m, 0.20m },
            new[] { 0.65m, 0.35m },
            new[] { 1.00m }
        };
        foreach (var depot in seed.Depots)
        {
            var fundCount = depotFundCounts[(depot.Id - 1) % depotFundCounts.Length];
            var ratios = depotFundBalanceRatios[fundCount == 3 ? 0 : fundCount == 2 ? 1 : 2];
            var totalDepotBalance = 85_000_000 + depot.Id * 12_000_000;

            for (var fundIndex = 0; fundIndex < fundCount; fundIndex++)
            {
                var fundSourceType = fundIndex == 1
                    ? FundSourceType.SystemFund.ToString()
                    : FundSourceType.Campaign.ToString();
                var fundSourceId = fundSourceType == FundSourceType.SystemFund.ToString()
                    ? systemFund.Id
                    : seed.FundCampaigns[(depot.Id + fundIndex) % seed.FundCampaigns.Count].Id;

                _db.DepotFunds.Add(new DepotFund
                {
                    DepotId = depot.Id,
                    Balance = decimal.Round(totalDepotBalance * ratios[fundIndex], 0, MidpointRounding.AwayFromZero),
                    LastUpdatedAt = seed.AnchorUtc.AddHours(-fundIndex),
                    FundSourceType = fundSourceType,
                    FundSourceId = fundSourceId
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        var depotFunds = await _db.DepotFunds.OrderBy(f => f.Id).ToListAsync(cancellationToken);

        var seededDisbursements = new List<CampaignDisbursement>();

        for (var i = 0; i < 42; i++)
        {
            var depot = seed.Depots[i % seed.Depots.Count];
            var approved = i < 30;
            var rejected = i >= 36;
            var created = RandomEventUtc(seed, i + 500);
            seed.FundingRequests.Add(new FundingRequest
            {
                DepotId = depot.Id,
                RequestedBy = seed.Managers[i % seed.Managers.Count].Id,
                TotalAmount = 12_000_000 + (i % 10) * 4_500_000,
                Description = "Bổ sung thuốc, áo mưa, nước uống và vật tư vệ sinh cho đợt mưa lũ",
                AttachmentUrl = $"https://cdn.resq.vn/funding/fr-{i + 1:000}.xlsx",
                Status = approved ? "Approved" : rejected ? "Rejected" : "Pending",
                ApprovedCampaignId = approved ? seed.FundCampaigns[i % seed.FundCampaigns.Count].Id : null,
                ReviewedBy = approved || rejected ? seed.Admins[0].Id : null,
                ReviewedAt = approved || rejected ? created.AddHours(6) : null,
                RejectionReason = rejected ? "Chưa đủ báo giá kèm theo" : null,
                CreatedAt = created
            });
        }
        _db.FundingRequests.AddRange(seed.FundingRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.FundingRequests)
        {
            for (var j = 0; j < 4; j++)
            {
                var item = seed.ItemModels[(request.Id * 5 + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 350_000 + j * 120_000 : 18_000 + j * 7_000;
                var quantity = item.ItemType == "Reusable" ? 3 + j : 50 + j * 20;
                _db.FundingRequestItems.Add(new FundingRequestItem
                {
                    FundingRequestId = request.Id,
                    Row = j + 1,
                    ItemName = item.Name ?? "Vật phẩm cứu trợ",
                    CategoryCode = seed.Categories.First(c => c.Id == item.CategoryId).Code ?? "GENERAL",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    ItemType = item.ItemType ?? "Consumable",
                    TargetGroup = "Adult",
                    VolumePerUnit = item.VolumePerUnit ?? 0,
                    WeightPerUnit = item.WeightPerUnit ?? 0,
                    ReceivedDate = DateOnly.FromDateTime(request.CreatedAt),
                    ExpiredDate = item.ItemType == "Consumable" ? DateOnly.FromDateTime(request.CreatedAt.AddMonths(8)) : null,
                    Notes = "Dòng seed demo cho funding request"
                });
            }
        }

        foreach (var request in seed.FundingRequests.Where(r => r.Status == "Approved").Take(25))
        {
            var disbursement = new CampaignDisbursement
            {
                FundCampaignId = request.ApprovedCampaignId!.Value,
                DepotId = request.DepotId,
                Amount = request.TotalAmount,
                Purpose = $"Duyệt yêu cầu cấp quỹ #{request.Id}",
                Type = "FundingRequestApproval",
                FundingRequestId = request.Id,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = request.ReviewedAt ?? request.CreatedAt.AddHours(8)
            };
            _db.CampaignDisbursements.Add(disbursement);
            await _db.SaveChangesAsync(cancellationToken);
            seededDisbursements.Add(disbursement);

            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(request.Id + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 420_000 : 25_000;
                var quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40;
                _db.DisbursementItems.Add(new DisbursementItem
                {
                    CampaignDisbursementId = disbursement.Id,
                    ItemName = item.Name ?? "Vật phẩm",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    Note = "Mua theo kế hoạch giải ngân",
                    CreatedAt = disbursement.CreatedAt
                });
            }
        }

        var adminDisbursements = seed.FundCampaigns
            .Select((campaign, index) => new CampaignDisbursement
            {
                FundCampaignId = campaign.Id,
                DepotId = seed.Depots[(index * 2) % seed.Depots.Count].Id,
                Amount = 18_000_000m + (index % 5) * 4_000_000m,
                Purpose = "Admin chủ động cấp tiền cho kho theo kế hoạch dự phòng",
                Type = DisbursementType.AdminAllocation.ToString(),
                FundingRequestId = null,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(10, 0)).AddDays(28 + index % 5))
            })
            .ToList();

        _db.CampaignDisbursements.AddRange(adminDisbursements);
        seededDisbursements.AddRange(adminDisbursements);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(seededDisbursements.Select(disbursement => new FundTransaction
        {
            FundCampaignId = disbursement.FundCampaignId,
            Type = TransactionType.Allocation.ToString(),
            Direction = "out",
            Amount = disbursement.Amount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement.ToString(),
            ReferenceId = disbursement.Id,
            CreatedBy = disbursement.CreatedBy,
            CreatedAt = disbursement.CreatedAt
        }));

        var raisedByCampaign = donations
            .Where(d => d.FundCampaignId.HasValue && d.Amount.HasValue && d.Status == Status.Succeed.ToString())
            .GroupBy(d => d.FundCampaignId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount ?? 0m));

        var disbursedByCampaign = seededDisbursements
            .GroupBy(d => d.FundCampaignId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        foreach (var campaign in seed.FundCampaigns)
        {
            var totalRaised = raisedByCampaign.TryGetValue(campaign.Id, out var raised) ? raised : 0m;
            var totalDisbursed = disbursedByCampaign.TryGetValue(campaign.Id, out var disbursed) ? disbursed : 0m;

            campaign.TotalAmount = totalRaised;
            campaign.CurrentBalance = totalRaised - totalDisbursed;
        }

        foreach (var fund in depotFunds)
        {
            for (var i = 0; i < 10; i++)
            {
                var transactionType = (i % 5) switch
                {
                    0 => DepotFundTransactionType.Allocation.ToString(),
                    1 => DepotFundTransactionType.Deduction.ToString(),
                    2 => DepotFundTransactionType.PersonalAdvance.ToString(),
                    3 => DepotFundTransactionType.AdvanceRepayment.ToString(),
                    _ => DepotFundTransactionType.Refund.ToString()
                };

                _db.DepotFundTransactions.Add(new DepotFundTransaction
                {
                    DepotFundId = fund.Id,
                    TransactionType = transactionType,
                    Amount = 1_500_000 + fund.Id * 180_000 + i * 650_000,
                    ReferenceType = transactionType == DepotFundTransactionType.Deduction.ToString()
                        ? "VatInvoice"
                        : transactionType == DepotFundTransactionType.Allocation.ToString()
                            ? "CampaignDisbursement"
                            : "DepotFundAllocation",
                    ReferenceId = i + 1,
                    Note = transactionType switch
                    {
                        "Deduction" => "Thanh toán mua bổ sung hàng cứu trợ từ quỹ kho",
                        "PersonalAdvance" => "Cá nhân ứng trước cho kho khi cần nhập hàng nhanh",
                        "AdvanceRepayment" => "Kho hoàn trả một phần tiền ứng cá nhân",
                        "Refund" => "Hoàn quỹ sau đối soát chi phí",
                        _ => "Nhận giải ngân vào quỹ kho"
                    },
                    CreatedBy = seed.Managers[i % seed.Managers.Count].Id,
                    CreatedAt = seed.StartUtc.AddDays(220 + fund.Id * 3 + i)
                });
            }
        }

        _db.SystemFundTransactions.AddRange(Enumerable.Range(0, 25).Select(i => new SystemFundTransaction
        {
            SystemFundId = systemFund.Id,
            TransactionType = i % 4 == 0 ? "TransferOut" : "Income",
            Amount = 10_000_000 + i * 2_000_000,
            ReferenceType = i % 4 == 0 ? "CampaignDisbursement" : "Donation",
            ReferenceId = i + 1,
            Note = i % 4 == 0 ? "Cấp vốn bổ sung cho kho" : "Ghi nhận tiền vào quỹ hệ thống",
            CreatedBy = seed.Admins[0].Id,
            CreatedAt = seed.StartUtc.AddDays(250 + i * 20)
        }));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAuditAndHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        await SeedInventoryMovementHistoryAsync(seed, cancellationToken);

        foreach (var depot in seed.Depots)
        {
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT",
                DepotId = depot.Id,
                DangerRatio = 0.18m,
                WarningRatio = 0.35m,
                MinimumThreshold = 120,
                IsActive = true,
                UpdatedBy = seed.Managers[depot.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-depot.Id),
                RowVersion = 1
            });
        }

        foreach (var category in seed.Categories.Take(20))
        {
            var depot = seed.Depots[category.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_CATEGORY",
                DepotId = depot.Id,
                CategoryId = category.Id,
                DangerRatio = 0.15m,
                WarningRatio = 0.32m,
                MinimumThreshold = 200,
                IsActive = true,
                UpdatedBy = seed.Managers[category.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-category.Id),
                RowVersion = 1
            });
        }

        foreach (var item in seed.ItemModels.Take(30))
        {
            var depot = seed.Depots[item.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_ITEM",
                DepotId = depot.Id,
                ItemModelId = item.Id,
                DangerRatio = 0.12m,
                WarningRatio = 0.30m,
                MinimumThreshold = item.ItemType == "Reusable" ? 3 : 80,
                IsActive = true,
                UpdatedBy = seed.Managers[item.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-item.Id % 60),
                RowVersion = 1
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var configs = await _db.InventoryStockThresholdConfigs
            .Where(c => c.Id != 1)
            .OrderBy(c => c.Id)
            .Take(90)
            .ToListAsync(cancellationToken);
        foreach (var config in configs)
        {
            _db.InventoryStockThresholdConfigHistories.Add(new InventoryStockThresholdConfigHistory
            {
                ConfigId = config.Id,
                ScopeType = config.ScopeType,
                DepotId = config.DepotId,
                CategoryId = config.CategoryId,
                ItemModelId = config.ItemModelId,
                OldDangerRatio = 0.10m,
                NewDangerRatio = config.DangerRatio,
                OldWarningRatio = 0.25m,
                NewWarningRatio = config.WarningRatio,
                ChangedBy = seed.Managers[config.Id % seed.Managers.Count].Id,
                ChangedAt = seed.AnchorUtc.AddDays(-config.Id % 90),
                ChangeReason = "Mùa mưa bão cần mức dự trữ cao hơn",
                Action = "Update"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryMovementHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var vatInvoices = await _db.VatInvoices
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken);
        var requestItems = await _db.DepotSupplyRequestItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var missionItems = await _db.MissionItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var vatInvoiceIds = vatInvoices.Select(v => v.Id).ToArray();
        var itemModelsById = seed.ItemModels.ToDictionary(i => i.Id);
        var missionsById = seed.Missions.ToDictionary(m => m.Id);
        var lotsByInventoryId = seed.Lots
            .GroupBy(l => l.SupplyInventoryId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).ToList());
        var inventoriesByDepotItem = seed.Inventories
            .Where(i => i.DepotId.HasValue && i.ItemModelId.HasValue)
            .ToDictionary(i => (i.DepotId!.Value, i.ItemModelId!.Value));

        var consumablePlans = seed.Inventories
            .Where(i => i.DepotId.HasValue
                && i.ItemModelId.HasValue
                && itemModelsById.TryGetValue(i.ItemModelId.Value, out var itemModel)
                && string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                && lotsByInventoryId.ContainsKey(i.Id))
            .Select(i => new ConsumableInventoryHistoryPlan
            {
                Inventory = i,
                ItemModel = itemModelsById[i.ItemModelId!.Value],
                BaseLot = lotsByInventoryId[i.Id][0],
                PerformedBy = ManagerForDepot(seed, i.DepotId!.Value)
            })
            .ToDictionary(plan => plan.Inventory.Id);

        var transferLogCount = BuildCompletedTransferHistory(
            seed,
            requestItems,
            itemModelsById,
            inventoriesByDepotItem,
            consumablePlans);

        var missionExportTarget = 100 - transferLogCount;
        BuildMissionExportHistory(
            seed,
            missionItems,
            itemModelsById,
            missionsById,
            inventoriesByDepotItem,
            consumablePlans,
            missionExportTarget);

        BuildAdjustmentHistory(consumablePlans.Values.ToList());

        var inventoryLogs = new List<InventoryLog>(820);
        BuildConsumableInventoryHistory(seed, vatInvoiceIds, consumablePlans.Values.ToList(), inventoryLogs);
        BuildReusableInventoryHistory(seed, vatInvoiceIds, inventoryLogs);

        _db.InventoryLogs.AddRange(inventoryLogs);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private int BuildCompletedTransferHistory(
        DemoSeedContext seed,
        IReadOnlyList<DepotSupplyRequestItem> requestItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans)
    {
        var requestItemsByRequestId = requestItems
            .GroupBy(i => i.DepotSupplyRequestId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).ToList());
        var inboundCapacity = consumablePlans.Values.ToDictionary(plan => plan.Inventory.Id, plan => plan.FinalQuantity);
        var transferLogs = 0;

        foreach (var request in seed.SupplyRequests
                     .Where(r => string.Equals(r.SourceStatus, "Completed", StringComparison.Ordinal))
                     .OrderBy(r => r.Id))
        {
            if (transferLogs >= 100 || !requestItemsByRequestId.TryGetValue(request.Id, out var items))
            {
                continue;
            }

            foreach (var item in items)
            {
                if (transferLogs >= 100
                    || !itemModelsById.TryGetValue(item.ItemModelId, out var itemModel)
                    || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                    || !inventoriesByDepotItem.TryGetValue((request.SourceDepotId, item.ItemModelId), out var sourceInventory)
                    || !inventoriesByDepotItem.TryGetValue((request.RequestingDepotId, item.ItemModelId), out var destinationInventory)
                    || !consumablePlans.TryGetValue(sourceInventory.Id, out var sourcePlan)
                    || !consumablePlans.TryGetValue(destinationInventory.Id, out var destinationPlan))
                {
                    continue;
                }

                var remainingInboundCapacity = inboundCapacity[destinationPlan.Inventory.Id];
                var quantity = Math.Min(item.Quantity, 10 + item.Id % 18);
                quantity = Math.Min(quantity, Math.Max(0, Math.Min(32, remainingInboundCapacity / 4)));
                if (quantity < 6)
                {
                    continue;
                }

                var shippedAt = request.ShippedAt ?? request.CompletedAt ?? request.CreatedAt.AddHours(3);
                var completedAt = request.CompletedAt ?? shippedAt.AddHours(4);

                sourcePlan.OutboundEvents.Add(new ConsumableOutboundEvent
                {
                    ActionType = InventoryActionType.TransferOut.ToString(),
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = request.Id,
                    Quantity = quantity,
                    CreatedAt = shippedAt,
                    PerformedBy = request.ShippedBy ?? request.PreparedBy ?? sourcePlan.PerformedBy,
                    MissionId = null,
                    Note = $"Xuất chuyển {itemModel.Name} từ {request.SourceDepot?.Name ?? $"kho #{request.SourceDepotId}"} sang {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} theo phiếu #{request.Id}"
                });

                destinationPlan.InboundTransfers.Add(new ConsumableInboundTransferEvent
                {
                    Quantity = quantity,
                    SourceId = request.Id,
                    CreatedAt = completedAt,
                    PerformedBy = request.ConfirmedBy ?? request.CompletedBy ?? destinationPlan.PerformedBy,
                    ReceivedDate = completedAt,
                    ExpiredDate = sourcePlan.BaseLot.ExpiredDate,
                    Note = $"Nhận chuyển {itemModel.Name} tại {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} từ phiếu điều phối #{request.Id}"
                });

                inboundCapacity[destinationPlan.Inventory.Id] -= quantity;
                transferLogs += 2;
            }
        }

        return transferLogs;
    }

    private void BuildMissionExportHistory(
        DemoSeedContext seed,
        IReadOnlyList<MissionItem> missionItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<int, Mission> missionsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans,
        int missionExportTarget)
    {
        if (missionExportTarget <= 0)
        {
            return;
        }

        var added = 0;
        foreach (var missionItem in missionItems)
        {
            if (added >= missionExportTarget
                || missionItem.SourceDepotId is null
                || missionItem.ItemModelId is null
                || !itemModelsById.TryGetValue(missionItem.ItemModelId.Value, out var itemModel)
                || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                || !inventoriesByDepotItem.TryGetValue((missionItem.SourceDepotId.Value, missionItem.ItemModelId.Value), out var inventory)
                || !consumablePlans.TryGetValue(inventory.Id, out var plan)
                || missionItem.MissionId is null
                || !missionsById.TryGetValue(missionItem.MissionId.Value, out var mission)
                || string.Equals(mission.Status, "Planned", StringComparison.Ordinal))
            {
                continue;
            }

            var quantity = missionItem.AllocatedQuantity ?? missionItem.RequiredQuantity ?? 0;
            quantity = Math.Min(quantity, 14 + missionItem.Id % 24);
            if (quantity <= 0)
            {
                continue;
            }

            plan.OutboundEvents.Add(new ConsumableOutboundEvent
            {
                ActionType = InventoryActionType.Export.ToString(),
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                Quantity = quantity,
                CreatedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(25 + missionItem.Id % 40),
                PerformedBy = plan.PerformedBy,
                MissionId = mission.Id,
                Note = $"Xuất {itemModel.Name} cho mission #{mission.Id} thuộc cụm SOS #{mission.ClusterId}"
            });
            added++;
        }
    }

    private static void BuildAdjustmentHistory(IReadOnlyList<ConsumableInventoryHistoryPlan> plans)
    {
        foreach (var plan in plans
                     .OrderBy(p => p.Inventory.Id)
                     .Where(p => p.Inventory.Id % 3 == 0)
                     .Take(45))
        {
            var quantity = Math.Min(3 + plan.Inventory.Id % 8, Math.Max(2, Math.Max(1, plan.FinalQuantity / 30)));
            plan.Adjustments.Add(new ConsumableAdjustmentEvent
            {
                Quantity = quantity,
                CreatedAt = (plan.Inventory.LastStockedAt ?? plan.BaseLot.ReceivedDate ?? DateTime.UtcNow).AddDays(18 + plan.Inventory.Id % 40),
                PerformedBy = plan.PerformedBy,
                Note = $"Điều chỉnh giảm {plan.ItemModel.Name} sau kiểm kê do hư hỏng hoặc quá hạn"
            });
        }
    }

    private void BuildConsumableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        IReadOnlyList<ConsumableInventoryHistoryPlan> plans,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var plan in plans.OrderBy(p => p.Inventory.Id))
        {
            var inboundQuantity = plan.InboundTransfers.Sum(t => t.Quantity);
            var outboundQuantity = plan.OutboundEvents.Sum(t => t.Quantity) + plan.Adjustments.Sum(t => t.Quantity);
            var baseRemaining = Math.Max(0, plan.FinalQuantity - inboundQuantity);
            var baseQuantity = Math.Max(1, baseRemaining + outboundQuantity);
            var receivedDate = plan.BaseLot.ReceivedDate ?? seed.StartUtc.AddDays(120 + plan.Inventory.Id % 520);
            var expiredDate = plan.BaseLot.ExpiredDate ?? receivedDate.AddMonths(6 + plan.Inventory.Id % 15);
            var sourceType = string.Equals(plan.BaseLot.SourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal)
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = plan.BaseLot.SourceId ?? plan.Inventory.Id;

            plan.BaseLot.Quantity = baseQuantity;
            plan.BaseLot.RemainingQuantity = baseRemaining;
            plan.BaseLot.ReceivedDate = receivedDate;
            plan.BaseLot.ExpiredDate = expiredDate;
            plan.BaseLot.SourceType = sourceType;
            plan.BaseLot.SourceId = sourceId;
            plan.BaseLot.CreatedAt = receivedDate;
            plan.Inventory.LastStockedAt = plan.InboundTransfers.Count == 0
                ? receivedDate
                : plan.InboundTransfers.Max(t => t.CreatedAt);

            inventoryLogs.Add(new InventoryLog
            {
                DepotSupplyInventoryId = plan.Inventory.Id,
                SupplyInventoryLot = plan.BaseLot,
                VatInvoiceId = ResolveVatInvoiceId(vatInvoiceIds, sourceType, sourceId),
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = baseQuantity,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = plan.PerformedBy,
                Note = $"Nhập gốc {plan.ItemModel.Name} vào {plan.Inventory.Depot?.Name ?? $"kho #{plan.Inventory.DepotId}"}",
                ReceivedDate = receivedDate,
                ExpiredDate = expiredDate,
                CreatedAt = receivedDate
            });

            foreach (var outbound in plan.OutboundEvents.OrderBy(e => e.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = outbound.ActionType,
                    QuantityChange = outbound.Quantity,
                    SourceType = outbound.SourceType,
                    SourceId = outbound.SourceId,
                    MissionId = outbound.MissionId,
                    PerformedBy = outbound.PerformedBy,
                    Note = outbound.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = outbound.CreatedAt
                });
            }

            foreach (var adjustment in plan.Adjustments.OrderBy(a => a.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = InventoryActionType.Adjust.ToString(),
                    QuantityChange = -adjustment.Quantity,
                    SourceType = InventorySourceType.Adjustment.ToString(),
                    PerformedBy = adjustment.PerformedBy,
                    Note = adjustment.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = adjustment.CreatedAt
                });
            }

            foreach (var inbound in plan.InboundTransfers.OrderBy(t => t.CreatedAt))
            {
                var transferLot = new SupplyInventoryLot
                {
                    SupplyInventoryId = plan.Inventory.Id,
                    Quantity = inbound.Quantity,
                    RemainingQuantity = inbound.Quantity,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    CreatedAt = inbound.CreatedAt
                };

                seed.Lots.Add(transferLot);
                _db.SupplyInventoryLots.Add(transferLot);

                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = transferLot,
                    ActionType = InventoryActionType.TransferIn.ToString(),
                    QuantityChange = inbound.Quantity,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    PerformedBy = inbound.PerformedBy,
                    Note = inbound.Note,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    CreatedAt = inbound.CreatedAt
                });
            }
        }
    }

    private void BuildReusableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var reusableItem in seed.ReusableItems.OrderBy(item => item.Id))
        {
            var sourceType = reusableItem.Id % 3 == 0
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = reusableItem.Id % 3 == 0
                ? vatInvoiceIds[(reusableItem.Id - 1) % vatInvoiceIds.Count]
                : reusableItem.Id;
            var createdAt = reusableItem.CreatedAt ?? seed.StartUtc.AddDays(140 + reusableItem.Id % 480);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                VatInvoiceId = sourceType == InventorySourceType.Purchase.ToString()
                    ? sourceId
                    : null,
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = 1,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = ManagerForDepot(seed, reusableItem.DepotId ?? seed.Depots[reusableItem.Id % seed.Depots.Count].Id),
                Note = $"Nhập thiết bị {reusableItem.ItemModel?.Name ?? $"vật phẩm #{reusableItem.ItemModelId}"} vào kho ban đầu",
                ReceivedDate = createdAt,
                CreatedAt = createdAt
            });
        }

        var reusableMissionUnits = seed.ReusableItems
            .Where(item => item.DepotId.HasValue && !string.Equals(item.Status, "Maintenance", StringComparison.Ordinal))
            .OrderBy(item => item.Id)
            .Take(30)
            .ToList();
        var completedMissions = seed.Missions
            .Where(m => string.Equals(m.Status, "Completed", StringComparison.Ordinal))
            .OrderBy(m => m.Id)
            .ToList();

        for (var index = 0; index < reusableMissionUnits.Count && completedMissions.Count > 0; index++)
        {
            var reusableItem = reusableMissionUnits[index];
            var mission = completedMissions[index % completedMissions.Count];
            var performedBy = ManagerForDepot(seed, reusableItem.DepotId!.Value);
            var exportedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(35 + index);
            var returnedAt = (mission.CompletedAt ?? exportedAt.AddHours(5)).AddMinutes(-20 + index % 6);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Export.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Xuất {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} cho mission #{mission.Id}",
                CreatedAt = exportedAt
            });

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Return.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Nhận lại {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} sau mission #{mission.Id}",
                CreatedAt = returnedAt
            });
        }
    }

    private static Guid ManagerForDepot(DemoSeedContext seed, int depotId)
    {
        return seed.Managers[(depotId - 1) % seed.Managers.Count].Id;
    }

    private static Depot OperationalDepotForActivity(DemoSeedContext seed, int missionId, int step)
    {
        var operationalDepots = seed.Depots
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        return operationalDepots[(missionId + step) % operationalDepots.Count];
    }

    private static bool IsDepotClosureTestCandidate(Depot depot) =>
        string.Equals(depot.Name, DepotClosureTestDepotName, StringComparison.Ordinal);

    private static void EnsureEssentialDepotStock(DemoSeedContext seed, ItemModel lifeJacketModel, ItemModel blanketModel)
    {
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            EnsureDepotInventory(seed, depot.Id, lifeJacketModel.Id, EssentialLifeJacketQuantity(depotIndex), depotIndex);
            EnsureDepotInventory(seed, depot.Id, blanketModel.Id, EssentialBlanketQuantity(depotIndex), depotIndex);
        }
    }

    private static void EnsureDepotInventory(DemoSeedContext seed, int depotId, int itemModelId, int quantity, int depotIndex)
    {
        var inventory = seed.Inventories.FirstOrDefault(i => i.DepotId == depotId && i.ItemModelId == itemModelId);
        if (inventory is null)
        {
            seed.Inventories.Add(new SupplyInventory
            {
                DepotId = depotId,
                ItemModelId = itemModelId,
                Quantity = quantity,
                MissionReservedQuantity = Math.Min(quantity / 10, 8),
                TransferReservedQuantity = Math.Min(quantity / 12, 6),
                LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex),
                IsDeleted = false
            });
            return;
        }

        inventory.Quantity = quantity;
        inventory.MissionReservedQuantity = Math.Min(quantity / 10, 8);
        inventory.TransferReservedQuantity = Math.Min(quantity / 12, 6);
        inventory.LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex);
        inventory.IsDeleted = false;
    }

    private static void EnsureEssentialBlanketLots(DemoSeedContext seed, ItemModel blanketModel)
    {
        var lotInventoryIds = seed.Lots
            .Select(l => l.SupplyInventoryId)
            .ToHashSet();
        var blanketInventories = seed.Inventories
            .Where(i => i.ItemModelId == blanketModel.Id)
            .OrderBy(i => i.DepotId)
            .ToList();

        foreach (var inventory in blanketInventories)
        {
            if (lotInventoryIds.Contains(inventory.Id))
            {
                continue;
            }

            var received = seed.AnchorUtc.AddDays(-45 - (inventory.DepotId ?? 0));
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = inventory.Quantity ?? 0,
                RemainingQuantity = Math.Max(0, (inventory.Quantity ?? 0) - inventory.MissionReservedQuantity - inventory.TransferReservedQuantity),
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(18),
                SourceType = InventorySourceType.Donation.ToString(),
                SourceId = 4_000 + inventory.Id,
                CreatedAt = received
            });
        }
    }

    private static void EnsureLifeJacketReusableUnits(DemoSeedContext seed, ItemModel lifeJacketModel)
    {
        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            var targetQuantity = seed.Inventories
                .Where(i => i.DepotId == depot.Id && i.ItemModelId == lifeJacketModel.Id)
                .Select(i => i.Quantity ?? 0)
                .Single();
            var existingCount = seed.ReusableItems.Count(item =>
                item.DepotId == depot.Id && item.ItemModelId == lifeJacketModel.Id);

            for (var unitIndex = existingCount; unitIndex < targetQuantity; unitIndex++)
            {
                var serialNumber = $"LIFEJACKET-D{depot.Id:00}-{unitIndex + 1:000}";
                if (!existingSerials.Add(serialNumber))
                {
                    continue;
                }

                seed.ReusableItems.Add(new ReusableItem
                {
                    DepotId = depot.Id,
                    ItemModelId = lifeJacketModel.Id,
                    SerialNumber = serialNumber,
                    Status = unitIndex % 19 == 0 ? "Maintenance" : unitIndex % 11 == 0 ? "Reserved" : "Available",
                    Condition = unitIndex % 23 == 0 ? "Fair" : "Good",
                    Note = unitIndex % 19 == 0 ? "Kiểm tra định kỳ trước mùa mưa bão" : null,
                    CreatedAt = seed.AnchorUtc.AddDays(-90 + (depotIndex * 7 + unitIndex) % 60),
                    UpdatedAt = seed.AnchorUtc.AddDays(-((depotIndex + unitIndex) % 25)),
                    IsDeleted = false
                });
            }
        }
    }

    private static int EssentialLifeJacketQuantity(int depotIndex) =>
        50 + (35 + depotIndex * 13) % 51;

    private static int EssentialBlanketQuantity(int depotIndex) =>
        50 + (42 + depotIndex * 17) % 51;

    private static int? ResolveVatInvoiceId(IReadOnlyList<int> vatInvoiceIds, string sourceType, int? sourceId)
    {
        if (!string.Equals(sourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal) || vatInvoiceIds.Count == 0)
        {
            return null;
        }

        if (sourceId.HasValue && vatInvoiceIds.Contains(sourceId.Value))
        {
            return sourceId.Value;
        }

        return vatInvoiceIds[Math.Abs((sourceId ?? 1) - 1) % vatInvoiceIds.Count];
    }

    private static User CreateUser(string username, int roleId, int number, string lastName, string firstName, string password, SeedArea area, DemoSeedContext seed)
    {
        var rolePrefix = roleId switch
        {
            1 => "admin",
            2 => "coord",
            3 => "rescuer",
            4 => "manager",
            _ => "victim"
        };
        var location = Point(area.Lon + (number % 7 - 3) * 0.002, area.Lat + (number % 5 - 2) * 0.002);
        return new User
        {
            Id = StableGuid($"user-{rolePrefix}-{number:000}"),
            RoleId = roleId,
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            Phone = Phone(roleId, number),
            Password = password,
            Email = $"{username}@resq.vn",
            IsEmailVerified = number % 17 != 0,
            AvatarUrl = $"https://i.pravatar.cc/160?u={username}",
            Location = location,
            Address = $"{10 + number % 90} {area.Address}",
            Ward = area.Ward,
            Province = area.Province,
            CreatedAt = seed.StartUtc.AddDays(number * 3 % 900),
            UpdatedAt = seed.AnchorUtc.AddDays(-(number % 60)),
            IsBanned = false
        };
    }

    private static User CreateDemoVictimWithPin(DemoSeedContext seed)
    {
        var area = Area(0);
        var user = CreateUser(
            "victim.demo.374745872",
            5,
            999,
            "Lê",
            "Minh Anh",
            SeedConstants.DemoVictimPinPasswordHash,
            area,
            seed);

        user.Phone = "+84374745872";
        user.Email = "victim.demo.374745872@resq.vn";
        user.Address = "32 Nguyễn Huệ, phường Phú Hội, Huế";
        user.Ward = "Phú Hội";
        user.Province = "Thừa Thiên Huế";
        user.Location = Point(107.5948, 16.4642);
        user.CreatedAt = new DateTime(2026, 4, 18, 10, 45, 0, DateTimeKind.Utc);
        user.UpdatedAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        user.IsEmailVerified = true;

        return user;
    }

    private static IEnumerable<UserRelativeProfile> CreateDemoVictimRelativeProfiles(Guid userId, DemoSeedContext seed)
    {
        var createdAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        var relatives = new[]
        {
            new RelativeProfileSeed(
                "Châu",
                "+84972513978",
                "ELDERLY",
                "FEMALE",
                ["me_gia", "can_diu", "uu_tien_so_tan"],
                "Mẹ 72 tuổi, huyết áp cao, hay đau khớp gối.",
                "Cần người dìu khi đi bộ xa hoặc leo cầu thang.",
                "Ăn mềm, hạn chế muối và đường.",
                Json(new
                {
                    bloodType = "UNKNOWN",
                    allergyDetails = "Dị ứng nhẹ với một số thuốc giảm đau nhóm NSAID.",
                    allergyOptions = new[] { "MEDICATION" },
                    medicalDevices = new[] { "WALKING_CANE" },
                    medicalHistory = new[] { "BONE_FRACTURE", "JOINT_PAIN" },
                    mobilityStatus = "NEEDS_ASSISTANCE",
                    specialSituation = new
                    {
                        isSenior = true,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = new[] { "HYPERTENSION", "DIABETES" },
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc huyết áp buổi sáng", "Thuốc tiểu đường sau ăn" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Từng gãy xương cổ tay phải, đi lại chậm khi trời mưa.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "An",
                "+84908112233",
                "ADULT",
                "FEMALE",
                ["vo", "lien_he_chinh", "di_chuyen_duoc"],
                "Sức khỏe ổn định, có tiền sử hen nhẹ khi lạnh.",
                "Cần mang theo thuốc xịt hen dự phòng.",
                "Không ăn hải sản sống.",
                Json(new
                {
                    bloodType = "O",
                    allergyDetails = "Dị ứng hải sản sống.",
                    allergyOptions = new[] { "FOOD" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "ASTHMA" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc xịt hen dự phòng" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Hen nhẹ, thường xuất hiện khi thời tiết lạnh hoặc ẩm.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Thảo",
                "+84933668120",
                "ADULT",
                "FEMALE",
                ["chi_gai", "biet_so_cuu", "co_the_ho_tro"],
                "Chị gái sống gần nhà, có thể hỗ trợ chăm sóc người già.",
                null,
                "Không ăn cay.",
                Json(new
                {
                    bloodType = "B",
                    allergyDetails = "",
                    allergyOptions = Array.Empty<string>(),
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = Array.Empty<string>(),
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Khoa",
                "+84911224567",
                "ADULT",
                "MALE",
                ["em_trai", "can_lien_lac", "di_chuyen_duoc"],
                "Em trai thường đi làm xa, cần báo sớm khi có sơ tán.",
                "Cần hỗ trợ định vị nếu mất sóng điện thoại.",
                null,
                Json(new
                {
                    bloodType = "A",
                    allergyDetails = "",
                    allergyOptions = new[] { "DUST" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "MIGRAINE" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "Đôi khi đau nửa đầu khi thiếu ngủ.",
                    otherChronicCondition = ""
                }))
        };

        return relatives.Select((relative, index) => new UserRelativeProfile
        {
            Id = StableGuid($"demo-victim-relative-{index + 1}"),
            UserId = userId,
            DisplayName = relative.DisplayName,
            PhoneNumber = relative.PhoneNumber,
            PersonType = relative.PersonType,
            RelationGroup = "gia_dinh",
            Gender = relative.Gender,
            TagsJson = Json(relative.Tags),
            MedicalBaselineNote = relative.MedicalBaselineNote,
            SpecialNeedsNote = relative.SpecialNeedsNote,
            SpecialDietNote = relative.SpecialDietNote,
            MedicalProfileJson = relative.MedicalProfileJson,
            ProfileUpdatedAt = createdAt.AddMinutes(index),
            CreatedAt = createdAt.AddSeconds(index * 12),
            UpdatedAt = createdAt.AddSeconds(index * 12 + 4)
        });
    }

    private static bool IsRecentRescuerNumber(int number) =>
        number > TotalRescuerCount - RecentRescuerCount;

    private static int RecentRescuerIndex(int number) =>
        number - (TotalRescuerCount - RecentRescuerCount) - 1;

    private static DateTime RecentRescuerCreatedAt(DemoSeedContext seed, int recentIndex)
    {
        var anchorVietnamDate = seed.AnchorUtc.AddHours(7).Date;
        var dayOffset = -29 + recentIndex * 27 / Math.Max(1, RecentRescuerCount - 1);
        var localCreatedAt = anchorVietnamDate
            .AddDays(dayOffset)
            .AddHours(8 + recentIndex % 10)
            .AddMinutes(recentIndex * 17 % 60);

        return VnToUtc(localCreatedAt);
    }

    private static DateTime RecentRescuerApprovedAt(DemoSeedContext seed, DateTime? createdAt, int recentIndex)
    {
        var approvedAt = (createdAt ?? RecentRescuerCreatedAt(seed, recentIndex))
            .AddDays(1 + recentIndex % 3)
            .AddHours(2);

        return approvedAt <= seed.AnchorUtc
            ? approvedAt
            : seed.AnchorUtc.AddHours(-(recentIndex % 12 + 1));
    }

    private static string Phone(int roleId, int number)
    {
        var prefix = roleId switch
        {
            1 => 900,
            2 => 901,
            3 => 902,
            4 => 903,
            5 => 904,
            _ => 905
        };
        return roleId == 5
            ? $"+84{prefix}{number:000000}"
            : $"0{prefix}{number:000000}";
    }

    private static Guid StableGuid(string value)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private static DateTime VnToUtc(DateTime vietnamLocal)
    {
        return DateTime.SpecifyKind(vietnamLocal - TimeSpan.FromHours(7), DateTimeKind.Utc);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static Point Point(double longitude, double latitude)
    {
        return new Point(longitude, latitude) { SRID = 4326 };
    }

    private static Point? OffsetPoint(Point? point, double latOffset, double lonOffset)
    {
        if (point is null)
        {
            return null;
        }

        return Point(point.X + lonOffset, point.Y + latOffset);
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static DateTime RandomEventLocal(DemoSeedContext seed, int index)
    {
        var yearBucket = index % 10;
        var year = yearBucket < 2 ? 2023 : yearBucket < 5 ? 2024 : yearBucket < 8 ? 2025 : 2026;
        var seasonBucket = index % 20;
        int month;
        if (seasonBucket < 13 && year < 2026)
        {
            month = 9 + index % 4;
        }
        else if (seasonBucket < 17)
        {
            month = 1 + index % 3;
        }
        else
        {
            month = year == 2023 ? 4 + index % 5 : 4 + index % 5;
            if (year == 2026)
            {
                month = 4;
            }
        }

        if (year == 2023 && month < 4)
        {
            month = 4;
        }
        if (year == 2026 && month > 4)
        {
            month = 4;
        }

        var maxDay = year == 2026 && month == 4 ? 16 : DateTime.DaysInMonth(year, month);
        var minDay = year == 2023 && month == 4 ? 16 : 1;
        var day = minDay + index % (maxDay - minDay + 1);
        return new DateTime(year, month, day, index % 24, (index * 7) % 60, 0, DateTimeKind.Unspecified);
    }

    private static DateTime RandomEventUtc(DemoSeedContext seed, int index) => VnToUtc(RandomEventLocal(seed, index));

    private static SeedArea Area(int index)
    {
        var areas = new[]
        {
            new SeedArea("HUE", "Thừa Thiên Huế", "Phú Hội", "Lê Lợi, Huế", 16.4637, 107.5962),
            new SeedArea("HUE", "Thừa Thiên Huế", "Hương Sơ", "Nguyễn Văn Linh, Huế", 16.4952, 107.5860),
            new SeedArea("DNG", "Đà Nẵng", "Hải Châu", "2 Tháng 9, Đà Nẵng", 16.0471, 108.2188),
            new SeedArea("QTR", "Quảng Trị", "Đông Hà", "Lê Duẩn, Đông Hà", 16.8175, 107.1003),
            new SeedArea("QNM", "Quảng Nam", "Tam Kỳ", "Hùng Vương, Tam Kỳ", 15.5736, 108.4740),
            new SeedArea("QNM", "Quảng Nam", "Hội An", "Cửa Đại, Hội An", 15.8801, 108.3380),
            new SeedArea("QNG", "Quảng Ngãi", "Trần Phú", "Quang Trung, Quảng Ngãi", 15.1214, 108.8044)
        };
        return areas[index % areas.Length];
    }

    private static (string Last, string First) VietnameseName(int index)
    {
        var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
        var firstNames = new[]
        {
            "Anh Tuấn", "Khánh Vy", "Minh Châu", "Quang Hải", "Thảo Nguyên", "Hoài Nam", "Thanh Hằng", "Đức Anh", "Mai Lan", "Gia Huy",
            "Hồng Nhung", "Bảo Trâm", "Văn Đức", "Thanh Tâm", "Nhật Minh", "Phương Linh", "Mạnh Hùng", "Diệu Anh", "Quốc Bảo", "Ngọc Hà"
        };
        return (lastNames[index % lastNames.Length], firstNames[index % firstNames.Length]);
    }

    private static string FullName(User user) => $"{user.LastName} {user.FirstName}".Trim();

    private static string TeamName(int index)
    {
        var names = new[] { "Hương Giang", "Bạch Mã", "Sơn Trà", "Hải Vân", "Thạch Hãn", "Thu Bồn", "Trà Khúc", "Phú Bài" };
        return names[index % names.Length];
    }

    private static string TeamMemberRole(int index, string? teamType)
    {
        if (teamType == "Medical")
        {
            return index % 2 == 0 ? "Medic" : "Support";
        }

        if (teamType == "Transportation")
        {
            return index % 2 == 0 ? "Driver" : "Loader";
        }

        return index % 3 == 0 ? "Navigator" : "Rescuer";
    }

    private static string Slug(string value)
    {
        var normalized = value.ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("đ", "d", StringComparison.Ordinal);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '-')
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }

    private static List<User> GetDeployableRescuers(DemoSeedContext seed) =>
        seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();

    private static AssemblyPoint? GetHueStadiumAssemblyPoint(DemoSeedContext seed) =>
        seed.AssemblyPoints.FirstOrDefault(point =>
            string.Equals(point.Code, "AP-HUE-TD-241015", StringComparison.Ordinal)
            || string.Equals(point.Name, "Sân vận động Tự Do (Thừa Thiên Huế)", StringComparison.Ordinal));

    private static IEnumerable<ServiceZone> ServiceZones(DateTime now)
        => ServiceZoneSeedData.CreateZones(now);

    private static IReadOnlyList<ItemTemplate> BaseItemModels()
    {
        return
        [
            new("Food", "Mì tôm", "Mì ăn liền đóng gói dùng cứu trợ khẩn cấp", "gói", "Consumable", 0.8m, 0.075m),
            new("Food", "Sữa bột trẻ em", "Sữa bột dinh dưỡng dành cho trẻ em dưới 6 tuổi", "gói", "Consumable", 0.5m, 0.4m),
            new("Food", "Lương khô", "Lương khô năng lượng cao, bảo quản lâu dài", "thanh", "Consumable", 0.15m, 0.06m),
            new("Food", "Gạo sấy khô", "Gạo sấy khô ăn liền, chỉ cần thêm nước nóng", "gói", "Consumable", 0.6m, 0.5m),
            new("Food", "Cháo ăn liền", "Cháo ăn liền đóng gói, dễ tiêu hóa cho mọi lứa tuổi", "gói", "Consumable", 0.4m, 0.065m),
            new("Food", "Bánh mì khô", "Bánh mì khô bảo quản lâu, tiện lợi khi cứu trợ", "gói", "Consumable", 0.8m, 0.15m),
            new("Food", "Muối tinh", "Muối tinh tiêu chuẩn dùng chế biến thực phẩm", "gói", "Consumable", 0.2m, 0.25m),
            new("Food", "Đường cát trắng", "Đường cát trắng tinh luyện dùng pha chế và nấu ăn", "gói", "Consumable", 0.35m, 0.5m),
            new("Food", "Dầu ăn thực vật", "Dầu ăn thực vật đóng chai dùng chế biến thực phẩm", "chai", "Consumable", 1.2m, 1.0m),
            new("Food", "Thịt hộp đóng gói", "Thịt hộp đóng gói bảo quản lâu, giàu dinh dưỡng", "hộp", "Consumable", 0.5m, 0.35m),
            new("Water", "Nước tinh khiết", "Nước uống đóng chai 500ml phục vụ cấp phát", "chai", "Consumable", 0.6m, 0.52m),
            new("Water", "Nước lọc bình 20L", "Bình nước lọc 20 lít phục vụ sinh hoạt tập thể", "bình", "Consumable", 22.0m, 20.5m),
            new("Water", "Viên lọc nước khẩn cấp", "Viên lọc nước cầm tay, xử lý nước bẩn thành nước uống", "viên", "Consumable", 0.005m, 0.004m),
            new("Water", "Nước đóng thùng 24 chai", "Thùng 24 chai nước uống 500ml tiện phân phối", "thùng", "Consumable", 16.0m, 13.0m),
            new("Water", "Nước khoáng thiên nhiên 500ml", "Nước khoáng thiên nhiên đóng chai 500ml", "chai", "Consumable", 0.6m, 0.53m),
            new("Water", "Nước dừa đóng hộp", "Nước dừa tươi đóng hộp bổ sung điện giải", "hộp", "Consumable", 0.4m, 0.35m),
            new("Water", "Bột bù điện giải ORS", "Bột pha bù nước và điện giải cho người mất nước", "gói", "Consumable", 0.05m, 0.025m),
            new("Medical", "Thuốc hạ sốt Paracetamol 500mg", "Thuốc hạ sốt giảm đau cơ bản cho người lớn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dầu gió", "Dầu gió xanh dùng xoa bóp giảm đau, chống cảm", "chai", "Consumable", 0.04m, 0.035m),
            new("Medical", "Sắt & Vitamin tổng hợp", "Viên uống bổ sung sắt và vitamin tổng hợp", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Băng gạc y tế vô khuẩn", "Băng gạc vô khuẩn dùng băng bó vết thương", "cuộn", "Consumable", 0.15m, 0.05m),
            new("Medical", "Bông gòn y tế", "Bông gòn y tế vô khuẩn dùng vệ sinh và sơ cứu", "gói", "Consumable", 0.4m, 0.05m),
            new("Medical", "Thuốc kháng sinh Amoxicillin", "Thuốc kháng sinh phổ rộng điều trị nhiễm khuẩn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dung dịch sát khuẩn Betadine", "Dung dịch sát khuẩn Povidone-Iodine rửa vết thương", "chai", "Consumable", 0.15m, 0.12m),
            new("Medical", "Khẩu trang y tế 3 lớp", "Khẩu trang y tế dùng một lần, đóng gói vô khuẩn", "chiếc", "Consumable", 0.04m, 0.005m),
            new("Medical", "Bộ sơ cứu cơ bản", "Bộ sơ cứu gồm băng, gạc, kéo, kẹp và thuốc cơ bản", "bộ", "Consumable", 3.0m, 1.5m),
            new("Hygiene", "Băng vệ sinh", "Băng vệ sinh phụ nữ dùng một lần, đóng gói riêng", "miếng", "Consumable", 0.06m, 0.015m),
            new("Hygiene", "Xà phòng diệt khuẩn", "Xà phòng cục diệt khuẩn dùng vệ sinh cá nhân", "bánh", "Consumable", 0.12m, 0.1m),
            new("Hygiene", "Nước rửa tay khô", "Gel rửa tay khô diệt khuẩn nhanh, không cần nước", "chai", "Consumable", 0.3m, 0.28m),
            new("Hygiene", "Khăn ướt kháng khuẩn", "Khăn ướt kháng khuẩn tiện dụng, đóng gói 10 tờ", "gói", "Consumable", 0.25m, 0.1m),
            new("Hygiene", "Kem đánh răng", "Kem đánh răng kích thước nhỏ gọn phù hợp cứu trợ", "tuýp", "Consumable", 0.15m, 0.12m),
            new("Hygiene", "Bàn chải đánh răng", "Bàn chải đánh răng dùng một lần, đóng gói riêng", "chiếc", "Consumable", 0.06m, 0.02m),
            new("Hygiene", "Dầu gội đầu", "Dầu gội đầu gói nhỏ tiện lợi cho cứu trợ", "chai", "Consumable", 0.25m, 0.22m),
            new("Hygiene", "Khăn bông tắm", "Khăn bông tắm cỡ trung dùng vệ sinh cá nhân", "chiếc", "Consumable", 2.5m, 0.35m),
            new("Hygiene", "Giấy vệ sinh", "Giấy vệ sinh cuộn nhỏ tiêu chuẩn", "cuộn", "Consumable", 1.2m, 0.1m),
            new("Hygiene", "Tã dùng một lần", "Tã giấy dùng một lần cho trẻ em hoặc người già", "miếng", "Consumable", 0.5m, 0.06m),
            new("Clothing", "Áo mưa người lớn", "Áo mưa nhựa dùng một lần cho người lớn", "chiếc", "Consumable", 1.5m, 0.25m),
            new("Clothing", "Ủng cao su chống lũ", "Ủng cao su chống nước dùng đi lại trong vùng ngập", "đôi", "Consumable", 6.0m, 1.8m),
            new("Clothing", "Bộ quần áo trẻ em", "Bộ quần áo sạch kích thước trẻ em 3–12 tuổi", "bộ", "Consumable", 2.0m, 0.3m),
            new("Clothing", "Áo ấm người lớn", "Áo khoác giữ ấm dùng trong thời tiết lạnh", "chiếc", "Consumable", 4.0m, 0.7m),
            new("Clothing", "Bộ quần áo người lớn", "Bộ quần áo sạch kích thước người lớn", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Bộ quần áo người cao tuổi", "Bộ quần áo thoải mái phù hợp người cao tuổi", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Găng tay giữ ấm", "Găng tay len giữ ấm trong thời tiết lạnh", "đôi", "Consumable", 0.3m, 0.08m),
            new("Clothing", "Tất len giữ ấm", "Tất len dày giữ ấm chân trong mùa lạnh", "đôi", "Consumable", 0.2m, 0.06m),
            new("Clothing", "Mũ len", "Mũ len giữ ấm đầu trong thời tiết lạnh", "chiếc", "Consumable", 0.4m, 0.08m),
            new("Clothing", "Áo mưa trẻ em", "Áo mưa nhựa dùng một lần cho trẻ em", "chiếc", "Consumable", 1.0m, 0.18m),
            new("Shelter", "Lều bạt cứu trợ 4 người", "Lều bạt dã chiến sức chứa 4 người, chống nước", "chiếc", "Consumable", 30.0m, 8.0m),
            new("Shelter", "Tấm bạt che mưa đa năng", "Tấm bạt PE chống nước đa năng dùng che mưa nắng", "tấm", "Consumable", 5.0m, 1.5m),
            new("Shelter", "Túi ngủ giữ nhiệt", "Túi ngủ cách nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 10.0m, 1.8m),
            new("Shelter", "Đệm hơi dã chiến", "Đệm hơi gấp gọn dùng ngủ dã chiến", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Shelter", "Màn chống côn trùng", "Màn lưới chống muỗi và côn trùng khi ngủ", "chiếc", "Consumable", 2.0m, 0.4m),
            new("Shelter", "Bộ cọc và dây lều", "Bộ cọc kim loại và dây buộc để dựng lều", "bộ", "Reusable", 3.0m, 2.0m),
            new("Shelter", "Tấm bạt chống thấm", "Tấm bạt PE dày chống thấm nước dùng lót sàn lều", "tấm", "Consumable", 4.0m, 1.2m),
            new("Shelter", "Dây buộc đa năng", "Dây thừng đa năng dùng buộc, cố định vật dụng", "cuộn", "Reusable", 2.0m, 1.5m),
            new("Shelter", "Đèn LED dã chiến", "Đèn LED sạc dùng chiếu sáng dã chiến", "chiếc", "Reusable", 1.0m, 0.35m),
            new("Shelter", "Nến khẩn cấp", "Nến cháy lâu dùng chiếu sáng khi mất điện", "cây", "Consumable", 0.15m, 0.12m),
            new("RepairTools", "Búa đóng đinh", "Búa sắt đóng đinh dùng sửa chữa nhà cửa", "chiếc", "Reusable", 1.5m, 0.5m),
            new("RepairTools", "Đinh các loại", "Bộ đinh sắt các kích cỡ dùng sửa chữa", "gói", "Consumable", 0.3m, 0.5m),
            new("RepairTools", "Cưa tay đa năng", "Cưa tay gấp gọn dùng cắt gỗ và vật liệu", "chiếc", "Reusable", 3.0m, 0.6m),
            new("RepairTools", "Tua vít 2 đầu", "Tua vít 2 đầu dẹt và bake dùng sửa chữa", "chiếc", "Reusable", 0.3m, 0.15m),
            new("RepairTools", "Kìm cắt dây", "Kìm cắt dây thép và dây điện đa năng", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RepairTools", "Băng keo chống thấm", "Băng keo dán chống thấm nước cho mái và tường", "cuộn", "Consumable", 0.2m, 0.15m),
            new("RepairTools", "Dao đa năng dã chiến", "Dao gấp đa năng tích hợp nhiều công cụ", "chiếc", "Reusable", 0.2m, 0.2m),
            new("RepairTools", "Xẻng tay", "Xẻng tay gấp gọn dùng đào đắp trong cứu trợ", "chiếc", "Reusable", 4.0m, 1.2m),
            new("RepairTools", "Bao cát chống lũ", "Bao cát dùng đắp đê ngăn nước lũ tràn", "chiếc", "Reusable", 2.5m, 0.4m),
            new("RepairTools", "Bộ dụng cụ sửa chữa điện cơ bản", "Bộ dụng cụ sửa chữa điện gồm kìm, tua vít, băng keo", "bộ", "Reusable", 4.0m, 2.5m),
            new("RescueEquipment", "Áo phao cứu sinh", "Áo phao tiêu chuẩn phục vụ cứu hộ đường thủy", "chiếc", "Reusable", 8.0m, 1.2m),
            new("RescueEquipment", "Bình lọc nước dã chiến", "Bình lọc nước di động lọc nước bẩn thành nước sạch", "chiếc", "Reusable", 5.0m, 2.0m),
            new("RescueEquipment", "Can đựng nước 10L", "Can nhựa 10 lít chứa và vận chuyển nước sạch", "chiếc", "Reusable", 12.0m, 0.8m),
            new("RescueEquipment", "Túi đựng nước linh hoạt", "Túi nhựa dẻo đựng nước gấp gọn khi không sử dụng", "chiếc", "Reusable", 1.5m, 0.3m),
            new("RescueEquipment", "Nhiệt kế điện tử", "Nhiệt kế điện tử đo thân nhiệt nhanh chóng", "chiếc", "Reusable", 0.1m, 0.05m),
            new("RescueEquipment", "Xuồng cao su cứu hộ", "Xuồng cao su chuyên dụng cho nhiệm vụ cứu hộ lũ", "chiếc", "Reusable", 250.0m, 45.0m),
            new("RescueEquipment", "Dây thừng cứu sinh 30m", "Dây thừng dài 30m chịu lực cao dùng cứu hộ", "cuộn", "Reusable", 6.0m, 3.5m),
            new("RescueEquipment", "Phao tròn cứu sinh", "Phao tròn cứu sinh tiêu chuẩn ném cho nạn nhân", "chiếc", "Reusable", 20.0m, 2.5m),
            new("RescueEquipment", "Máy bơm nước di động", "Máy bơm nước chạy xăng di động hút nước ngập", "chiếc", "Reusable", 60.0m, 25.0m),
            new("RescueEquipment", "Bộ đàm liên lạc dã chiến", "Bộ đàm cầm tay liên lạc tần số UHF/VHF", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RescueEquipment", "Đèn tín hiệu khẩn cấp", "Đèn tín hiệu nhấp nháy cảnh báo khu vực nguy hiểm", "chiếc", "Reusable", 0.8m, 0.4m),
            new("RescueEquipment", "Máy phát điện di động", "Máy phát điện xăng di động công suất nhỏ", "chiếc", "Reusable", 120.0m, 50.0m),
            new("RescueEquipment", "Cáng khiêng thương", "Cáng gấp gọn dùng vận chuyển người bị thương", "chiếc", "Reusable", 30.0m, 7.0m),
            new("RescueEquipment", "Mũ bảo hiểm cứu hộ", "Mũ bảo hiểm chuyên dụng cho cứu hộ viên", "chiếc", "Reusable", 6.0m, 0.6m),
            new("Heating", "Chăn ấm giữ nhiệt", "Chăn dày giữ nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 6.0m, 1.5m),
            new("Heating", "Than tổ ong", "Than tổ ong dùng đốt sưởi ấm hoặc nấu ăn", "viên", "Consumable", 1.2m, 1.0m),
            new("Heating", "Máy sưởi điện mini", "Máy sưởi điện nhỏ gọn công suất thấp", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Heating", "Túi sưởi ấm tay dùng một lần", "Túi sưởi ấm tay phản ứng hóa học dùng một lần", "gói", "Consumable", 0.05m, 0.04m),
            new("Heating", "Bộ quần áo nhiệt", "Bộ đồ lót giữ nhiệt mặc trong thời tiết rét", "bộ", "Consumable", 2.5m, 0.4m),
            new("Heating", "Ấm đun nước du lịch", "Ấm đun nước điện nhỏ gọn tiện dùng dã chiến", "chiếc", "Consumable", 3.0m, 0.8m),
            new("Heating", "Bếp gas du lịch mini", "Bếp gas mini gấp gọn dùng nấu ăn dã chiến", "chiếc", "Consumable", 4.0m, 1.5m),
            new("Heating", "Bình gas mini dã chiến", "Bình gas lon nhỏ dùng cho bếp gas du lịch", "bình", "Consumable", 0.8m, 0.35m),
            new("Heating", "Chăn điện sưởi", "Chăn điện sưởi ấm dùng khi ngủ mùa lạnh", "chiếc", "Consumable", 5.0m, 1.8m),
            new("Heating", "Tấm sưởi ấm bức xạ", "Tấm sưởi hồng ngoại bức xạ di động", "chiếc", "Consumable", 15.0m, 5.0m),
            new("Vehicle", "Xe tải cứu trợ 2.5 tấn", "Xe tải 2.5 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 18000.0m, 3500.0m),
            new("Vehicle", "Xe cứu thương", "Xe chuyên dụng vận chuyển cấp cứu và bệnh nhân", "chiếc", "Reusable", 16000.0m, 3800.0m),
            new("Vehicle", "Xe bán tải 4x4", "Xe bán tải 2 cầu vượt địa hình xấu", "chiếc", "Reusable", 12000.0m, 2200.0m),
            new("Vehicle", "Xe máy địa hình", "Xe máy địa hình đi vào vùng khó tiếp cận", "chiếc", "Reusable", 2500.0m, 150.0m),
            new("Vehicle", "Ca nô cứu hộ", "Ca nô máy chuyên dụng cứu hộ đường thủy", "chiếc", "Reusable", 8000.0m, 800.0m),
            new("Vehicle", "Xe chở hàng nhẹ 1 tấn", "Xe tải nhẹ 1 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 14000.0m, 2500.0m),
            new("Vehicle", "Xe tải đông lạnh 3.5 tấn", "Xe tải đông lạnh bảo quản thực phẩm tươi sống", "chiếc", "Reusable", 20000.0m, 5000.0m),
            new("Vehicle", "Xe khách 16 chỗ", "Xe khách 16 chỗ chở người sơ tán", "chiếc", "Reusable", 15000.0m, 3200.0m),
            new("Vehicle", "Xe cẩu di động", "Xe cẩu di động dọn dẹp đổ nát và vật cản", "chiếc", "Reusable", 20000.0m, 12000.0m),
            new("Vehicle", "Xe chuyên dụng phòng cháy", "Xe chữa cháy chuyên dụng phòng cháy chữa cháy", "chiếc", "Reusable", 18000.0m, 8000.0m),
            new("Others", "Pin dự phòng 10000mAh", "Pin sạc dự phòng 10000mAh sạc điện thoại", "chiếc", "Consumable", 0.25m, 0.22m),
            new("Others", "Cáp sạc đa năng", "Cáp sạc đa đầu Lightning/USB-C/Micro USB", "chiếc", "Consumable", 0.08m, 0.04m),
            new("Others", "Bản đồ địa hình khẩn cấp", "Bản đồ in địa hình khu vực thường xảy ra thiên tai", "tờ", "Consumable", 0.1m, 0.05m),
            new("Others", "Còi báo động khẩn cấp", "Còi thổi báo động và kêu gọi cứu hộ khẩn cấp", "chiếc", "Consumable", 0.02m, 0.015m),
            new("Others", "Kính bảo hộ lao động", "Kính bảo hộ chống bụi và mảnh vỡ khi làm việc", "chiếc", "Reusable", 0.3m, 0.08m),
            new("Others", "Ba lô khẩn cấp", "Ba lô chứa đồ dùng thiết yếu cho tình huống khẩn cấp", "chiếc", "Consumable", 25.0m, 0.8m),
            new("Others", "Sổ tay và bút ghi chép", "Bộ sổ tay và bút bi dùng ghi chép thông tin hiện trường", "bộ", "Consumable", 0.3m, 0.18m),
            new("Others", "Bộ đèn pin đội đầu", "Đèn pin LED đội đầu rọi sáng rảnh tay", "bộ", "Reusable", 0.5m, 0.15m),
            new("Others", "Áo phản quang an toàn", "Áo ghi lê phản quang tăng nhận diện trong đêm", "chiếc", "Reusable", 1.5m, 0.2m),
            new("Others", "Pháo sáng khẩn cấp", "Pháo sáng phát tín hiệu cầu cứu khẩn cấp", "chiếc", "Consumable", 0.25m, 0.15m)
        ];
    }

    private static IReadOnlyList<int> ReliefItemImageIdsInSeedOrder()
    {
        return
        [
            1, 7, 8, 11, 12, 13, 14, 15, 16, 17,
            2, 18, 19, 20, 22, 25, 26,
            3, 9, 10, 27, 28, 29, 30, 32, 33,
            5, 34, 35, 36, 37, 38, 39, 40, 41, 42,
            43, 44, 45, 46, 47, 48, 49, 50, 51, 52,
            53, 54, 55, 56, 57, 58, 59, 60, 61, 62,
            63, 64, 65, 66, 67, 68, 69, 70, 71, 72,
            4, 21, 23, 24, 31, 73, 74, 75, 76, 77, 78, 79, 80, 81,
            6, 82, 83, 84, 85, 86, 87, 88, 89, 90,
            101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
            91, 92, 93, 94, 95, 96, 97, 98, 99, 100
        ];
    }

    private static string? GetReliefItemImageUrl(int id)
    {
        return id switch
        {
            1 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/001-mi-tom_n1u4fq.jpg",
            2 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/002-nuoc-tinh-khiet_xlky5f.png",
            3 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/003-thuoc-ha-sot-paracetamol-500mg_yaeovi.jpg",
            4 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774866312/004-ao-phao-cuu-sinh_ozit6b.jpg",
            5 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/005-bang-ve-sinh_yhudge.png",
            6 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/006-chan-am-giu-nhiet_ivibn8.png",
            7 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/007-sua-bot-tre-em_vzydxc.png",
            8 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/008-luong-kho_xhokm0.png",
            9 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/009-dau-gio_rbndq6.jpg",
            10 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/010-sat-vitamin-tong-hop_rtdjgu.png",
            11 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/011-gao-say-kho_urtmri.jpg",
            12 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/012-chao-an-lien_rgwjcq.jpg",
            13 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/013-banh-mi-kho_xe7rew.jpg",
            14 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/014-muoi-tinh_odzyix.png",
            15 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/015-duong-cat-trang_vfhuvv.png",
            16 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/016-dau-an-thuc-vat_l41nwp.jpg",
            17 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/017-thit-hop-dong-goi_xrvcnj.png",
            18 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/018-nuoc-loc-binh-20l_xyk8mp.png",
            19 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/019-vien-loc-nuoc-khan-cap_jrezrb.jpg",
            20 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/020-nuoc-dong-thung-24-chai_ktfzck.jpg",
            21 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/021-binh-loc-nuoc-da-chien_gy22py.jpg",
            22 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/022-nuoc-khoang-thien-nhien-500ml_fcjxnc.jpg",
            23 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/023-can-dung-nuoc-10l_bkqljt.png",
            24 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/024-tui-dung-nuoc-linh-hoat_zpizku.jpg",
            25 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/025-nuoc-dua-dong-hop_t0ytn2.png",
            26 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/026-bot-bu-dien-giai-ors_s47y7a.jpg",
            27 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/027-bang-gac-y-te-vo-khuan_c2mkww.jpg",
            28 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/028-bong-gon-y-te_jb2euw.png",
            29 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/029-thuoc-khang-sinh-amoxicillin_hes4wt.png",
            30 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/030-dung-dich-sat-khuan-betadine_zhbkce.jpg",
            31 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/031-nhiet-ke-dien-tu_wxgjdw.png",
            32 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/032-khau-trang-y-te-3-lop_darfut.jpg",
            33 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/033-bo-so-cuu-co-ban_ws83xn.png",
            34 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/034-xa-phong-diet-khuan_g09ho0.png",
            35 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/035-nuoc-rua-tay-kho_bxhmvl.jpg",
            36 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/036-khan-uot-khang-khuan_wwoh14.png",
            37 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/037-kem-danh-rang_s2ibzl.jpg",
            38 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/038-ban-chai-danh-rang_vd42ax.png",
            39 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/039-dau-goi-dau_o9njdq.jpg",
            40 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/040-khan-bong-tam_o94plx.png",
            41 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/041-giay-ve-sinh_c3fryk.jpg",
            42 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/042-ta-dung-mot-lan_yixozm.jpg",
            43 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/043-ao-mua-nguoi-lon_fc7kry.jpg",
            44 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/044-ung-cao-su-chong-lu_lz9qbw.jpg",
            45 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/045-bo-quan-ao-tre-em_n4agu9.jpg",
            46 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/046-ao-am-nguoi-lon_ma6thc.jpg",
            47 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/047-bo-quan-ao-nguoi-lon_umzueu.png",
            48 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/048-bo-quan-ao-nguoi-cao-tuoi_por2xe.jpg",
            49 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/049-gang-tay-giu-am_k56rfm.jpg",
            50 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/050-tat-len-giu-am_ov0jjd.jpg",
            51 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/051-mu-len_wzipsi.jpg",
            52 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865757/052-ao-mua-tre-em_b0mocf.jpg",
            53 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/053-leu-bat-cuu-tro-4-nguoi_qj8w9i.png",
            54 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/054-tam-bat-che-mua-da-nang_xvvydi.jpg",
            55 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/055-tui-ngu-giu-nhiet_mnhbww.jpg",
            56 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/056-dem-hoi-da-chien_ns7izi.jpg",
            57 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/057-man-chong-con-trung_iip3fn.jpg",
            58 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/058-bo-coc-va-day-leu_ywukij.jpg",
            59 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/059-tam-bat-chong-tham_ensdzn.jpg",
            60 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/060-day-buoc-da-nang_mpzo8n.jpg",
            61 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/061-den-led-da-chien_hcylgj.jpg",
            62 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/062-nen-khan-cap_fwzazj.png",
            63 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/063-bua-dong-dinh_ulqde0.jpg",
            64 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/064-dinh-cac-loai_k7fsm9.jpg",
            65 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/065-cua-tay-da-nang_jopzf5.jpg",
            66 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/066-tua-vit-2-dau_tzzrzx.jpg",
            67 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/067-kim-cat-day_tiq6jt.jpg",
            68 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/068-bang-keo-chong-tham_bbctyd.jpg",
            69 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/069-dao-da-nang-da-chien_n68ore.jpg",
            70 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/070-xeng-tay_ktfrdj.jpg",
            71 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/071-bao-cat-chong-lu_cvey61.jpg",
            72 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/072-bo-dung-cu-sua-chua-dien-co-ban_k2peyh.jpg",
            73 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/073-xuong-cao-su-cuu-ho_t3gcxt.jpg",
            74 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/074-day-thung-cuu-sinh-30m_nepsc3.png",
            75 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/075-phao-tron-cuu-sinh_fosz4i.jpg",
            76 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/076-may-bom-nuoc-di-dong_npf0tr.jpg",
            77 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/077-bo-dam-lien-lac-da-chien_kwbfsm.jpg",
            78 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/078-den-tin-hieu-khan-cap_o3frpt.jpg",
            79 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/078-den-tin-hieu-khan-cap_yp3mui.jpg",
            80 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/080-cang-khieng-thuong_xszlmj.jpg",
            81 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/081-mu-bao-hiem-cuu-ho_qetnbw.jpg",
            82 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/082-than-to-ong_m7sdry.jpg",
            83 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/083-may-suoi-dien-mini_hy0wg4.png",
            84 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/084-tui-suoi-am-tay-dung-mot-lan_sadxtb.jpg",
            85 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/085-bo-quan-ao-nhiet_wxsmmj.jpg",
            86 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/086-am-dun-nuoc-du-lich_vbh2ap.jpg",
            87 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/087-bep-gas-du-lich-mini_zeyjrk.jpg",
            88 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/088-binh-gas-mini-da-chien_yeapzn.jpg",
            89 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865734/089-chan-dien-suoi_kvul8o.jpg",
            90 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/090-tam-suoi-am-buc-xa_tysxho.png",
            91 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/091-pin-du-phong-10000mah_gczx45.jpg",
            92 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/092-cap-sac-da-nang_knsvuy.jpg",
            93 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/093-ban-do-dia-hinh-khan-cap_pm5zkt.jpg",
            94 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/094-coi-bao-dong-khan-cap_ukvhal.png",
            95 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/095-kinh-bao-ho-lao-dong_wl8n1f.jpg",
            96 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/096-ba-lo-khan-cap_jn7icq.jpg",
            97 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/097-so-tay-va-but-ghi-chep_h9lums.jpg",
            98 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/098-bo-den-pin-doi-dau_ucnidx.jpg",
            99 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/099-ao-phan-quang-an-toan_trpgia.jpg",
            100 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/100-phao-sang-khan-cap_t0nxwi.jpg",
            101 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/101-xe-tai-cuu-tro-2-5-tan_ifxbqk.jpg",
            102 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/102-xe-cuu-thuong_zqevrt.png",
            103 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/103-xe-ban-tai-4x4_wrs2t4.png",
            104 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/104-xe-may-dia-hinh_xphh0x.png",
            105 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/105-ca-no-cuu-ho_lzudkx.jpg",
            106 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/106-xe-cho-hang-nhe-1-tan_rrmaie.png",
            107 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/107-xe-tai-dong-lanh-3-5-tan_ttxps8.jpg",
            108 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/108-xe-khach-16-cho_h3tjcc.jpg",
            109 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/109-xe-cau-di-dong_xcphgy.jpg",
            110 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/110-xe-chuyen-dung-phong-chay_xoomtb.jpg",
            _ => null
        };
    }

    private static IReadOnlyList<string> TargetGroupNamesFor(ItemTemplate template)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static bool HasAny(string value, params string[] patterns) =>
            patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        void Add(params string[] names)
        {
            foreach (var name in names)
            {
                groups.Add(name);
            }
        }

        switch (template.CategoryCode)
        {
            case "FOOD":
            case "WATER":
            case "MEDICINE":
            case "HYGIENE":
            case "CLOTHING":
            case "SHELTER":
            case "HEATING":
            case "OTHERS":
                Add("Adult");
                break;
            case "REPAIR_TOOLS":
            case "RESCUE_EQUIPMENT":
            case "VEHICLE":
                Add("Rescuer");
                break;
        }

        if (HasAny(template.Name, "trẻ em"))
        {
            Add("Children");
        }

        if (HasAny(template.Name, "người cao tuổi"))
        {
            Add("Elderly");
        }

        if (HasAny(template.Name, "Băng vệ sinh", "Sắt & Vitamin"))
        {
            Add("Pregnant");
        }

        if (HasAny(template.Name, "Cháo ăn liền", "Chăn ấm giữ nhiệt"))
        {
            Add("Children", "Elderly", "Pregnant");
        }

        if (HasAny(template.Name, "Nước tinh khiết", "Bột bù điện giải ORS"))
        {
            Add("Children", "Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Gạo sấy khô")
        {
            Add("Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Tã dùng một lần")
        {
            Add("Children", "Elderly");
        }

        if (template.CategoryCode == "FOOD" && HasAny(template.Name, "Mì tôm", "Lương khô", "Bánh mì khô", "Thịt hộp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "WATER" && HasAny(template.Name, "Viên lọc nước khẩn cấp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "MEDICINE" && HasAny(template.Name, "Băng gạc", "Bông gòn", "Betadine", "Khẩu trang", "Bộ sơ cứu"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HYGIENE" && HasAny(template.Name, "Nước rửa tay khô", "Khăn ướt kháng khuẩn"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "CLOTHING" && HasAny(template.Name, "Áo mưa người lớn", "Ủng cao su chống lũ"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "SHELTER" && (template.ItemType == "Reusable" || HasAny(template.Name, "Lều bạt", "Tấm bạt chống thấm", "Nến khẩn cấp")))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HEATING" && HasAny(template.Name, "Túi sưởi", "Bếp gas", "Bình gas"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "OTHERS" && HasAny(template.Name, "Pin dự phòng", "Cáp sạc", "Bản đồ", "Còi báo động", "Ba lô", "Bộ đèn pin", "Áo phản quang", "Kính bảo hộ", "Pháo sáng"))
        {
            Add("Rescuer");
        }

        if (groups.Count == 0)
        {
            Add("Adult");
        }

        return groups.ToList();
    }

    private static string Situation(int index)
    {
        var situations = new[] { "Flooding", "Landslide", "Stranded", "CannotMove", "Medical", "NeedSupplies", "Evacuation" };
        return situations[index % situations.Length];
    }

    private static string[] SuppliesFor(string situation)
    {
        return situation switch
        {
            "Medical" => ["Medicine", "Water"],
            "NeedSupplies" => ["Water", "Food", "Blanket"],
            "Evacuation" => ["Lifejacket", "Water"],
            _ => ["Water", "Food", "Medicine"]
        };
    }

    private static string SosMessage(string situation, int people, bool injured)
    {
        var injury = injured ? " có người bị thương nhẹ," : "";
        return situation switch
        {
            "Flooding" => $"Nhà tôi đang ngập sâu,{injury} có {people} người cần xuồng vào hỗ trợ.",
            "Landslide" => $"Sạt lở sau nhà, đường bị chắn,{injury} gia đình {people} người đang mắc kẹt.",
            "Stranded" => $"Chúng tôi bị cô lập trên tầng hai,{injury} cần đội cứu hộ tiếp cận.",
            "CannotMove" => $"Có người già không thể di chuyển,{injury} nước đang lên nhanh.",
            "Medical" => $"Có ca y tế cần hỗ trợ,{injury} cần thuốc và sơ cứu tại chỗ.",
            "NeedSupplies" => $"Khu sơ tán thiếu nước uống và thức ăn cho {people} người.",
            _ => $"Cần sơ tán {people} người ra khỏi vùng ngập bằng xuồng hoặc xe tải."
        };
    }

    private static double PriorityScore(string priority, int index)
    {
        return priority switch
        {
            "Critical" => 88 + index % 12,
            "High" => 68 + index % 15,
            "Medium" => 42 + index % 18,
            _ => 20 + index % 18
        };
    }

    private static string MissionType(int index, string? severity)
    {
        if (severity == "Critical" && index % 2 == 0)
        {
            return "Mixed";
        }

        var types = new[] { "Rescue", "Medical", "Supply", "Mixed" };
        return types[index % types.Length];
    }

    private RescueTeam TeamForMission(DemoSeedContext seed, int missionIndex, int teamOffset)
    {
        var missionType = seed.Missions[missionIndex].MissionType;
        var required = missionType switch
        {
            "Medical" => "Medical",
            "Supply" => "Transportation",
            "Mixed" => "Mixed",
            _ => "Rescue"
        };
        var candidates = seed.RescueTeams
            .Where(t => t.TeamType == required && t.Status is "Available" or "Gathering")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        candidates = seed.RescueTeams
            .Where(t => t.TeamType == required && t.Status != "Disbanded" && t.Status != "Unavailable")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        return seed.RescueTeams[(missionIndex + teamOffset) % seed.RescueTeams.Count];
    }

    private static void SyncRescueTeamStatusesFromAssignments(DemoSeedContext seed)
    {
        var activeMissionTeamsByRescueTeam = seed.MissionTeams
            .Where(team => team.RescuerTeamId.HasValue && team.UnassignedAt is null && team.Status != "Cancelled")
            .GroupBy(team => team.RescuerTeamId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var rescueTeam in seed.RescueTeams)
        {
            if (rescueTeam.Status is "Disbanded" or "Unavailable" or "Stuck")
            {
                continue;
            }

            if (!activeMissionTeamsByRescueTeam.TryGetValue(rescueTeam.Id, out var missionTeams))
            {
                rescueTeam.Status = rescueTeam.Status == "Gathering" ? "Gathering" : "Available";
                continue;
            }

            rescueTeam.Status = missionTeams.Any(team => team.Status == "InProgress")
                ? "OnMission"
                : missionTeams.Any(team => team.Status == "Assigned")
                    ? "Assigned"
                    : "Available";
        }
    }

    private static string ActivityType(int step, int total, string? missionType)
    {
        if (step == 1)
        {
            return "COLLECT_SUPPLIES";
        }

        if (step == total)
        {
            return "RETURN_SUPPLIES";
        }

        if (step == 2 && missionType is "Supply" or "Mixed")
        {
            return "DELIVER_SUPPLIES";
        }

        if (missionType == "Medical")
        {
            return "MEDICAL_AID";
        }

        return step % 2 == 0 ? "EVACUATE" : "RESCUE";
    }

    private static string ActivityStatusFor(string? missionStatus, int step, int total)
    {
        return missionStatus switch
        {
            "Completed" => "Succeed",
            "OnGoing" => step == 1 ? "Succeed" : step == total ? "Planned" : "OnGoing",
            "Incompleted" => step == total ? "Failed" : "Succeed",
            _ => "Planned"
        };
    }

    private static string ActivityDescription(string type, string? depotName, string? sosMessage)
    {
        return type switch
        {
            "COLLECT_SUPPLIES" => $"Di chuyển đến {depotName}, nhận nước uống, thuốc và áo phao.",
            "DELIVER_SUPPLIES" => "Giao vật phẩm cho hộ dân theo danh sách SOS.",
            "RETURN_SUPPLIES" => $"Hoàn trả áo phao, bộ đàm và dây cứu sinh về {depotName}.",
            "MEDICAL_AID" => "Sơ cứu tại chỗ, kiểm tra huyết áp và chuyển tuyến nếu cần.",
            "EVACUATE" => "Đưa người già, trẻ em ra điểm tránh trú an toàn.",
            _ => sosMessage ?? "Tiếp cận hiện trường và hỗ trợ cứu hộ."
        };
    }

    private static string IncidentDescription(int index)
    {
        var descriptions = new[]
        {
            "Xuồng bị kẹt rác ở chân cầu, cần hỗ trợ kéo ra.",
            "Đường vào khu dân cư nước chảy xiết, đội tạm dừng chờ điều phối.",
            "Một rescuer bị trượt chân xây xát nhẹ.",
            "Phát hiện thêm hộ dân bị cô lập phía sau trường mầm non.",
            "Bộ đàm mất tín hiệu trong 15 phút do mưa lớn."
        };
        return descriptions[index % descriptions.Length];
    }

    private static string IncidentType(int index)
    {
        var types = new[] { "VehicleIssue", "UnsafeRoute", "RescuerInjury", "AdditionalVictimsFound", "CommunicationLost" };
        return types[index % types.Length];
    }

    private static string SosUpdateContent(string? status)
    {
        return status switch
        {
            "Resolved" => "Đội cứu hộ xác nhận đã hỗ trợ xong và cập nhật an toàn.",
            "InProgress" => "Đội cứu hộ đang trên đường, ETA khoảng 20 phút.",
            "Assigned" => "Đã phân công đội phụ trách tiếp cận hiện trường.",
            "Cancelled" => "Yêu cầu đã hủy sau khi xác minh an toàn.",
            _ => "Đang chờ điều phối viên xác nhận thêm thông tin."
        };
    }

    private static string ChatMessage(int index, string? status)
    {
        var active = status == "CoordinatorActive";
        var messages = new[]
        {
            "Hệ thống đã ghi nhận yêu cầu hỗ trợ.",
            "Tôi đã đọc thông tin SOS, bạn hãy giữ điện thoại khô và bật âm lượng.",
            "Nhà em còn một bà cụ không đi lại được, nước đang lên nhanh.",
            active ? "Đội cứu hộ đang di chuyển từ điểm tập kết gần nhất." : "Tôi đang chờ điều phối viên phản hồi.",
            "Nếu có thể, hãy tập trung mọi người ở vị trí cao nhất trong nhà.",
            "Gia đình còn nước uống khoảng nửa ngày.",
            "Đã bổ sung nhu cầu nước uống và thuốc vào ghi chú mission.",
            "Có trẻ nhỏ nên cần áo phao cỡ nhỏ khi tiếp cận.",
            "Tín hiệu hơi yếu, tôi sẽ gửi vị trí lại.",
            "Đã nhận vị trí, sai số khoảng dưới 30m.",
            "Khi thấy đội cứu hộ, hãy dùng đèn pin hoặc khăn sáng màu để báo hiệu.",
            "Cảm ơn, gia đình sẽ chờ ở tầng hai.",
            "Cuộc hội thoại được lưu để điều phối tiếp theo.",
            "Ảnh hiện trường: https://cdn.resq.vn/chat/flood-demo.jpg"
        };
        return messages[index % messages.Length];
    }

    private static string SupplyRequestNote(int index)
    {
        var notes = new[]
        {
            "Thiếu nước uống và thuốc hạ sốt cho đợt lũ Quảng Điền.",
            "Cần bổ sung áo phao, dây cứu sinh cho đội xuồng.",
            "Kho địa phương gần cạn lương khô và sữa trẻ em.",
            "Xin điều chuyển bộ đàm và pin dự phòng trước bão.",
            "Cần máy phát điện và đèn sạc cho điểm tránh trú."
        };
        return notes[index % notes.Length];
    }

    private static string OrganizationName(int index)
    {
        var names = new[]
        {
            "Nhóm thiện nguyện Hướng về miền Trung",
            "Công ty nước sạch Sông Hương",
            "Hội Chữ thập đỏ Đà Nẵng",
            "Quỹ cộng đồng Bạch Mã",
            "Câu lạc bộ xe bán tải miền Trung",
            "Công ty thiết bị cứu hộ An Tâm",
            "Nhóm Bếp ấm vùng lũ"
        };
        return names[index % names.Length] + (index >= 7 ? $" {index + 1}" : "");
    }

    private static string SupplierName(int index)
    {
        var suppliers = new[]
        {
            "Công ty TNHH Thiết bị cứu hộ An Tâm",
            "Công ty CP Nước uống Sông Hương",
            "Nhà thuốc Trung tâm Huế",
            "Công ty TNHH Lương thực miền Trung",
            "Công ty CP Vật tư y tế Đà Nẵng"
        };
        return suppliers[index % suppliers.Length];
    }

    private static string CampaignName(int index)
    {
        var names = new[]
        {
            "Chiến dịch hỗ trợ bão Noru miền Trung",
            "Chiến dịch lũ sớm Huế - Quảng Trị",
            "Chiến dịch tiếp sức vùng sạt lở Trà My",
            "Chiến dịch nước sạch sau lũ Quảng Ngãi",
            "Chiến dịch áo phao cho vùng ngập sâu"
        };
        return names[index % names.Length] + $" #{index + 1}";
    }

    private sealed class ConsumableInventoryHistoryPlan
    {
        public required SupplyInventory Inventory { get; init; }
        public required ItemModel ItemModel { get; init; }
        public required SupplyInventoryLot BaseLot { get; init; }
        public required Guid PerformedBy { get; init; }
        public int FinalQuantity => Inventory.Quantity ?? 0;
        public List<ConsumableOutboundEvent> OutboundEvents { get; } = [];
        public List<ConsumableInboundTransferEvent> InboundTransfers { get; } = [];
        public List<ConsumableAdjustmentEvent> Adjustments { get; } = [];
    }

    private sealed class ConsumableOutboundEvent
    {
        public required string ActionType { get; init; }
        public required string SourceType { get; init; }
        public int? SourceId { get; init; }
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public int? MissionId { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableInboundTransferEvent
    {
        public required int Quantity { get; init; }
        public int? SourceId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public DateTime? ReceivedDate { get; init; }
        public DateTime? ExpiredDate { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableAdjustmentEvent
    {
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public required string Note { get; init; }
    }

    private sealed record SeedArea(string Code, string Province, string Ward, string Address, double Lat, double Lon);

    private sealed record RelativeProfileSeed(
        string DisplayName,
        string PhoneNumber,
        string PersonType,
        string Gender,
        IReadOnlyList<string> Tags,
        string? MedicalBaselineNote,
        string? SpecialNeedsNote,
        string? SpecialDietNote,
        string MedicalProfileJson);

    private sealed record ItemTemplate(
        string CategoryCode,
        string Name,
        string Description,
        string Unit,
        string ItemType,
        decimal Volume,
        decimal Weight);
}
