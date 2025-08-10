using Microsoft.AspNetCore.SignalR;
using TechHubComm.Hub;
using TechHubComm.Services;
using TechHubComm.Models;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
using static TechHubComm.Services.Interfaces;
using TechHubComm.Health;

var builder = WebApplication.CreateBuilder(args);

// Redis Configuration for Scale-Out
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "VirtualBoard";
});

// Redis for SignalR backplane
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(configuration);
});

// SignalR with Redis backplane for horizontal scaling
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.StreamBufferCapacity = 10;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"), options =>
{
    options.Configuration.ChannelPrefix = "VirtualBoard";
});

// Services
builder.Services.AddSingleton<IVirtualBoardService, TechHubComm.Services.VirtualBoardService>();
builder.Services.AddSingleton<IConnectionManager, RedisConnectionManager>();
builder.Services.AddSingleton<IMessageBroker, RedisMessageBroker>();
builder.Services.AddSingleton<IBoardHistoryService, RedisBoardHistoryService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<VirtualBoardHealthCheck>("virtual-board")
    .AddRedis(builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddSingleton<IMessageBroker, RedisMessageBroker>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();

// Scoped - Per request/hub invocation, can maintain state during request
builder.Services.AddScoped<IVirtualBoardService, VirtualBoardService>();
builder.Services.AddScoped<IConnectionManager, RedisConnectionManager>();
builder.Services.AddScoped<IBoardHistoryService, RedisBoardHistoryService>();

// Metrics and monitoring
builder.Services.AddSingleton<IMetricsService, MetricsService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors("AllowAll");
app.UseHealthChecks("/health");

//app.MapHub<VirtualBoardHub>("/virtualboadhub/{classroomId}/{subjectId}/{topicId}");

app.Run();
