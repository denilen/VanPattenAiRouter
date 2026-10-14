using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;

var builder = WebApplication.CreateBuilder(args);

    // Explicitly load configuration files from content root (fixes Docker mounting issues)
    var contentRoot = builder.Environment.ContentRootPath;
    builder.Configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile(Path.Combine(contentRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true);

    // Configure JSON options for minimal API model binding
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy
        = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy
        = JsonNamingPolicy.CamelCase;
});

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
                                                          .ReadFrom.Configuration(context.Configuration)
                                                          .ReadFrom.Services(services)
                                                          .Enrich.FromLogContext()
                                                          .Enrich.WithMachineName()
                                                          .Enrich.WithThreadId()
                                                          .Enrich.WithProcessId());

// Configuration is automatically loaded by CreateBuilder:
    // - appsettings.json
    // - appsettings.{Environment}.json (e.g., appsettings.Development.json)
    // - Environment variables, command line args, etc.
    // Hot reload is enabled by default for both files.

    // Configure options
builder.Services.Configure<AppOptions>(builder.Configuration);
builder.Services.Configure<RouterOptions>(builder.Configuration.GetSection("Router"));
builder.Services.Configure<RetryConfig>(builder.Configuration.GetSection("Router:Retry"));
builder.Services.AddSingleton<RetryPolicyService>();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for browser/extension access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure database - use InMemory for testing, SQLite for production
builder.Services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();
var appOptions = builder.Configuration.Get<AppOptions>() ?? new AppOptions();
var useInMemory = appOptions.UseInMemoryDatabase;
if (useInMemory)
{
    builder.Services.AddDbContext<AiRouterDbContext>(options =>
                                                         options.UseInMemoryDatabase("VanPattenAIRouterTest"));
}
else
{
    // Configure SQLite database
    var dbPath = appOptions.Database.Path
        ?? Path.Combine(AppContext.BaseDirectory, "vanpatten-airrouter.db");
    if (!Path.IsPathRooted(dbPath)) dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
    builder.Services.AddDbContext<AiRouterDbContext>(options =>
                                                         options.UseSqlite($"Data Source={dbPath}"));
}

// Register services
builder.Services.AddScoped<IProviderService, ProviderService>();
builder.Services.AddScoped<IRoutingService, RoutingService>();
builder.Services.AddHttpClient("streaming", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan; // No timeout for streaming
    client.DefaultRequestVersion = new Version(2, 0);
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});
builder.Services.AddHttpClient("default", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120); // 2 min timeout for non-streaming
    client.DefaultRequestVersion = new Version(2, 0);
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});
builder.Services.AddHttpClient(); // Default client with default timeout

// Register background services
builder.Services.AddHostedService<ModelRefreshService>();

var app = builder.Build();

    // Ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AiRouterDbContext>();
        var appOptionsMonitor = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();
        var dbOptions = appOptionsMonitor.Value.Database;

        if (dbOptions.RecreateOnStartup)
        {
            Log.Information("RecreateOnStartup is enabled - deleting existing database");
            db.Database.EnsureDeleted();
        }

        db.Database.EnsureCreated();

        // Seed default providers if none exist
        await SeedData.InitializeAsync(db, appOptionsMonitor);
    }
    Log.Information("SeedData completed");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// OpenAI compatible endpoints
app.MapPost("/v1/chat/completions", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<ChatCompletionRequest>(body, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    if (request?.Stream == true)
    {
        // Streaming response
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        await routingService.RouteChatCompletionStreamAsync(request!, apiKey, context.Response.Body,
                                                            context.RequestAborted);
        return Results.Empty;
    }

    var response = await routingService.RouteChatCompletionAsync(request!, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/models", async (IProviderService providerService, HttpContext context) =>
{
    var grouped = context.Request.Query.TryGetValue("grouped", out var groupedVal) &&
        (groupedVal == "true" || groupedVal == "1");

    if (grouped)
    {
        var groupedModels = await providerService.GetGroupedModelsAsync();
        return Results.Ok(groupedModels);
    }

    var models = await providerService.GetAvailableModelsAsync();
    return Results.Ok(new { data = models, @object = "list" });
});

app.MapPost("/v1/embeddings", async (EmbeddingRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteEmbeddingAsync(request, apiKey);
    return Results.Ok(response);
});

// Admin endpoints for managing providers
app.MapGet("/admin/providers", async (IProviderService providerService) =>
{
    var providers = await providerService.GetAllProvidersAsync();
    return Results.Ok(providers);
});

app.MapPost("/admin/providers", async (ProviderDto provider, IProviderService providerService) =>
{
    var created = await providerService.CreateProviderAsync(provider);
    return Results.Created($"/admin/providers/{created.Id}", created);
});

app.MapPut("/admin/providers/{id}", async (int id, ProviderDto provider, IProviderService providerService) =>
{
    var updated = await providerService.UpdateProviderAsync(id, provider);
    return updated != null ? Results.Ok(updated) : Results.NotFound();
});

app.MapDelete("/admin/providers/{id}", async (int id, IProviderService providerService) =>
{
    await providerService.DeleteProviderAsync(id);
    return Results.NoContent();
});

try
{
    Log.Information("Starting VanPattenAIRouter...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "VanPattenAIRouter terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

namespace VanPattenAIRouter
{
    /// <summary>
    ///     Program class for the VanPatten AI Router application.
    /// </summary>
    /// <remarks>
    ///     This is the entry point for the application. It configures services, middleware,
    ///     and API endpoints for an OpenAI-compatible AI routing service that can route
    ///     requests to multiple providers with failover and rate limiting.
    /// </remarks>
    public class Program
    {
    }
}