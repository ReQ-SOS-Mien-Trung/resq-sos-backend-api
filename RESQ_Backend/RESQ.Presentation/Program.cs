using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Services;
using RESQ.Infrastructure.Extensions;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Presentation.Extensions;
using RESQ.Presentation.Hubs;
using RESQ.Presentation.Middlewares;
using RESQ.Presentation.Services;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (EF.IsDesignTime)
{
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.None);
}

// Controllers + JSON enum
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddTransient<GlobalExceptionMiddleware>();

// Add CORS - AllowCredentials is required for SignalR WebSocket handshake
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .SetIsOriginAllowed(_ => true) // allow any origin
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // required for SignalR
});

// Add SignalR
builder.Services.AddSignalR();

// Register NotificationHubService (Presentation implementation of Application interface)
builder.Services.AddScoped<INotificationHubService, NotificationHubService>();
builder.Services.AddScoped<IDashboardHubService, DashboardHubService>();
builder.Services.AddScoped<IOperationalHubService, OperationalHubService>();

//jwt swagger
// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Swagger + JWT support
builder.Services.AddSwaggerGen(c =>
{
    // Dùng full type name làm schemaId để tránh xung đột khi có 2 class cùng tên ở namespace khác nhau
    c.CustomSchemaIds(type => type.FullName);

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RESQ API",
        Version = "v1",
        Description = "RESQ Backend API with JWT authentication"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Render enum properties as string names in Swagger (mirrors JsonStringEnumConverter)
    c.SchemaFilter<RESQ.Presentation.Extensions.EnumSchemaFilter>();

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// Health check
builder.Services.AddHealthChecks();

// Firebase Admin SDK initialization - đọc từ appsettings section "Firebase"
if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
{
    var fb = builder.Configuration.GetSection("Firebase");

    // Replace literal \n (2 chars) thành newline thật - phòng trường hợp configuration
    // không unescape JSON escape sequences (xảy ra trên một số cloud platforms)
    var privateKey = (fb["PrivateKey"] ?? "").Replace("\\n", "\n");

    // Dùng Dictionary để đảm bảo tên key JSON được giữ nguyên, không bị naming policy đổi
    var credentialDict = new Dictionary<string, string?>
    {
        ["type"]                        = fb["Type"],
        ["project_id"]                  = fb["ProjectId"],
        ["private_key_id"]              = fb["PrivateKeyId"],
        ["private_key"]                 = privateKey,
        ["client_email"]                = fb["ClientEmail"],
        ["client_id"]                   = fb["ClientId"],
        ["auth_uri"]                    = fb["AuthUri"],
        ["token_uri"]                   = fb["TokenUri"],
        ["auth_provider_x509_cert_url"] = fb["AuthProviderX509CertUrl"],
        ["client_x509_cert_url"]        = fb["ClientX509CertUrl"],
        ["universe_domain"]             = fb["UniverseDomain"]
    };

    var credentialJson = System.Text.Json.JsonSerializer.Serialize(credentialDict);

    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(credentialJson));
    var firebaseCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromStream(stream);

    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
    {
        Credential = firebaseCredential
    });
}

// Dependency Injection
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// -- Memory cache (dùng bởi PermissionAuthorizationHandler) --------------
builder.Services.AddMemoryCache();

// -- Dynamic Permission Authorization ------------------------------------
builder.Services.AddPermissionAuthorization();


// JWT CONFIGURATION
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // .NET 8 mặc định dùng JsonWebTokenHandler với MapInboundClaims = false,
    // khiến claim "sub" trong JWT KHÔNG được map thành ClaimTypes.NameIdentifier.
    // Bật lại để User.FindFirst(ClaimTypes.NameIdentifier) hoạt động đúng.
    options.MapInboundClaims = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey)
        ),

        ClockSkew = TimeSpan.Zero
    };

    // Allow SignalR to receive JWT via query string
    // (WebSocket & Server-Sent Events cannot send Authorization header)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Chỉ chạy migrate + seed khi được yêu cầu rõ ràng qua flag (ví dụ: Railway release command).
// Khi start app bình thường, DB phải đã sẵn sàng — không tự migrate để tránh conflict.
if (IsDatabaseSeedOnlyMode(args))
{
    InitializeDatabase(app);
    return;
}

// Middleware pipeline

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RESQ API v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

// 7. Map SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<OperationalHub>("/hubs/operational");

app.Run();

static bool IsDatabaseSeedOnlyMode(string[] args)
{
    return args.Any(arg =>
        string.Equals(arg, "seed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--seed-only", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--migrate-seed", StringComparison.OrdinalIgnoreCase));
}

static void InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
    var hasMigrations = dbContext.Database.GetMigrations().Any();

    if (hasMigrations)
    {
        // Guard against the "relation already exists" error that occurs when the schema
        // was partially applied (e.g. app crashed mid-migration) but EF never recorded
        // the migration in __EFMigrationsHistory.
        // Strategy: if our sentinel sequence exists → schema is already in place →
        // mark all un-recorded migrations as applied before calling Migrate().
        RecoverPartialMigrations(dbContext);
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }

    // Gọi seeder SAU migrate/EnsureCreated để tránh EF nested execution strategy conflict
    RESQ.Infrastructure.Extensions.ServiceCollectionExtensions.RunSeedAsync(dbContext)
        .GetAwaiter().GetResult();
}

// If the schema already exists but __EFMigrationsHistory is missing or incomplete
// (e.g. after a crash between DDL execution and history write), insert the missing
// migration records so EF's Migrate() skips those DDL statements.
static void RecoverPartialMigrations(ResQDbContext dbContext)
{
    try
    {
        // Check for our sentinel object that gets created early in the first migration.
        var conn = dbContext.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM information_schema.sequences
            WHERE sequence_schema = 'public'
              AND sequence_name    = 'depot_realtime_version_seq'";
        var exists = (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        conn.Close();

        if (!exists) return; // Fresh DB — let Migrate() do its job normally.

        // Schema is already present. Ensure __EFMigrationsHistory exists and
        // contains records for every migration that's already been applied.
        dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId""    character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32)  NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            )");

        var productVersion = (typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "9.0.0").Split('+')[0];

        foreach (var migrationId in dbContext.Database.GetMigrations())
        {
            // migrationId is an internal EF-generated constant (e.g. "20260417103131_AddNewSeeds"),
            // not user input — safe to interpolate directly.
#pragma warning disable EF1002
            dbContext.Database.ExecuteSqlRaw($"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{migrationId}', '{productVersion}')
                ON CONFLICT DO NOTHING
                """);
#pragma warning restore EF1002
        }
    }
    catch
    {
        // Non-fatal: if this check itself fails, let Migrate() attempt normally.
    }
}

public partial class Program;
