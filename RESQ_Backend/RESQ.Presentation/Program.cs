using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RESQ.Application.Extensions;
using RESQ.Infrastructure.Extensions;
using RESQ.Presentation.Extensions;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Presentation.Hubs;
using RESQ.Presentation.Middlewares;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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

// Add CORS — AllowCredentials is required for SignalR WebSocket handshake
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

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// Health check
builder.Services.AddHealthChecks();

// Firebase Admin SDK initialization
var firebaseKeyPath = Path.Combine(builder.Environment.ContentRootPath, "PRM PE 142 Firebase Admin SDK.json");
if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
{
    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
    {
        Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(firebaseKeyPath)
    });
}

// Dependency Injection
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// ── Memory cache (dùng bởi PermissionAuthorizationHandler) ──────────────
builder.Services.AddMemoryCache();

// ── Dynamic Permission Authorization ────────────────────────────────────
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

// Auto-apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
  await db.Database.MigrateAsync();
  // Seed permissions and role-permission mappings (idempotent)
  await PermissionSeeder.SeedAsync(db);
}

// Seed test inventory-movement data for development testing
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<ResQDbContext>();
    try { await RuntimeDataSeeder.SeedInventoryMovementTestDataAsync(seedDb); }
    catch (Exception ex) { Console.WriteLine($"[RuntimeDataSeeder] Skipped: {ex.Message}"); }
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

app.Run();
app.Run();
