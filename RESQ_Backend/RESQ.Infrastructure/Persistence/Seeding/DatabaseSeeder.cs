using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder : IDatabaseSeeder
{
    private const string MarkerName = "demo-seed-v6-2026-04-29";

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
        await SeedReferenceDataAsync(cancellationToken);
        await SeedAiSuggestionsAsync(cancellationToken);
        await ApplyLogisticsSeedCorrectionsAsync(cancellationToken);

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
                await SeedAiSuggestionsAsync(cancellationToken);
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
}
