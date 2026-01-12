using RESQ.Application.Common.Interfaces;
using RESQ.Infrastructure.Caching;
using RESQ.Infrastructure.Notifications;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Redis Options
var redisOptions = new RedisOptions();
builder.Configuration.GetSection(RedisOptions.SectionName).Bind(redisOptions);
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

// 2. Register Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisOptions.ConnectionString));

// 3. Register Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisOptions.ConnectionString;
    options.InstanceName = redisOptions.InstanceName;
});

// 4. Configure SignalR with Redis Backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisOptions.ConnectionString, options => {
        options.Configuration.ChannelPrefix = "RESQ_NOTIFICATIONS";
    });

// 5. Register Services (Tất cả từ Infrastructure)
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddRedis(redisOptions.ConnectionString, name: "redis");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// 6. Map Endpoints
app.MapHealthChecks("/health");
app.MapControllers();

// Map Hub từ Infrastructure (Presentation có quyền biết Infrastructure)
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
