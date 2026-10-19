using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Middleware;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;

var builder = WebApplication.CreateBuilder(args);

// Explicitly load configuration files from content root (fixes Docker mounting issues)
var contentRoot = builder.Environment.ContentRootPath;
builder.Configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), false, true);
builder.Configuration.AddJsonFile(Path.Combine(contentRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"),
                                  true, true);

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

// Add OpenAI-compatible response headers middleware
app.UseOpenAiHeaders();

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

app.MapGet("/v1/models/{**modelId}", async (string modelId, IProviderService providerService) =>
{
    var models = await providerService.GetAvailableModelsAsync();
    var model = models.FirstOrDefault(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));

    if (model == null)
        return Results.NotFound(new
        {
            error = new { message = $"Model '{modelId}' not found", type = "not_found_error", code = "model_not_found" }
        });

    return Results.Ok(model);
});

app.MapPost("/v1/embeddings", async (EmbeddingRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteEmbeddingAsync(request, apiKey);
    return Results.Ok(response);
});

// Legacy Completions API
app.MapPost("/v1/completions", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<CompletionRequest>(body, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    if (request?.Stream == true)
    {
        // Streaming response
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await routingService.RouteCompletionStreamAsync(request!, apiKey, context.Response.Body,
                                                        context.RequestAborted);
        return Results.Empty;
    }

    var response = await routingService.RouteCompletionAsync(request!, apiKey);
    return Results.Ok(response);
});

// Moderations API
app.MapPost("/v1/moderations", async (ModerationRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteModerationAsync(request, apiKey);
    return Results.Ok(response);
});

// Images API (DALL-E)
app.MapPost("/v1/images/generations",
            async (ImageGenerationRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteImageGenerationAsync(request, apiKey);
                return Results.Ok(response);
            });

app.MapPost("/v1/images/edits", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    // Handle multipart/form-data
    var form = await context.Request.ReadFormAsync();

    var request = new ImageEditRequest
    {
        Prompt = form["prompt"].FirstOrDefault() ?? "",
        Model = form["model"].FirstOrDefault(),
        N = int.TryParse(form["n"].FirstOrDefault(), out var n) ? n : null,
        Size = form["size"].FirstOrDefault(),
        ResponseFormat = form["response_format"].FirstOrDefault(),
        User = form["user"].FirstOrDefault()
    };

    if (form.Files.GetFile("image") != null)
    {
        using var ms = new MemoryStream();
        await form.Files["image"].CopyToAsync(ms);
        request.Image = ms.ToArray();
    }

    if (form.Files.GetFile("mask") != null)
    {
        using var ms = new MemoryStream();
        await form.Files["mask"].CopyToAsync(ms);
        request.Mask = ms.ToArray();
    }

    var response = await routingService.RouteImageEditAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapPost("/v1/images/variations", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    // Handle multipart/form-data
    var form = await context.Request.ReadFormAsync();

    var request = new ImageVariationRequest
    {
        Model = form["model"].FirstOrDefault(),
        N = int.TryParse(form["n"].FirstOrDefault(), out var n) ? n : null,
        Size = form["size"].FirstOrDefault(),
        ResponseFormat = form["response_format"].FirstOrDefault(),
        User = form["user"].FirstOrDefault()
    };

    if (form.Files.GetFile("image") != null)
    {
        using var ms = new MemoryStream();
        await form.Files["image"].CopyToAsync(ms);
        request.Image = ms.ToArray();
    }

    var response = await routingService.RouteImageVariationAsync(request, apiKey);
    return Results.Ok(response);
});

// Audio API (Whisper + TTS)
app.MapPost("/v1/audio/transcriptions", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var form = await context.Request.ReadFormAsync();

    var request = new AudioTranscriptionRequest
    {
        Model = form["model"].FirstOrDefault(),
        Language = form["language"].FirstOrDefault(),
        Prompt = form["prompt"].FirstOrDefault(),
        ResponseFormat = form["response_format"].FirstOrDefault(),
        Temperature = double.TryParse(form["temperature"].FirstOrDefault(), out var temp) ? temp : null
    };

    var timestampGranularities = form["timestamp_granularities[]"].FirstOrDefault();
    if (!string.IsNullOrEmpty(timestampGranularities))
        request.TimestampGranularities = timestampGranularities.Split(',').ToList();
    else if (form.ContainsKey("timestamp_granularities"))
        request.TimestampGranularities = form["timestamp_granularities"].ToList();

    if (form.Files.GetFile("file") != null)
    {
        using var ms = new MemoryStream();
        await form.Files["file"].CopyToAsync(ms);
        request.File = ms.ToArray();
        request.FileName = form.Files["file"].FileName;
    }

    var response = await routingService.RouteAudioTranscriptionAsync(request, apiKey);

    // Handle different response formats
    var responseFormat = form["response_format"].FirstOrDefault() ?? "json";
    if (responseFormat == "text") return Results.Text(response.Text, "text/plain");

    if (responseFormat == "srt" || responseFormat == "vtt")
        // For SRT/VTT, we'd need to format the response - for now return JSON
        return Results.Ok(response);

    return Results.Ok(response);
});

app.MapPost("/v1/audio/translations", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var form = await context.Request.ReadFormAsync();

    var request = new AudioTranslationRequest
    {
        Model = form["model"].FirstOrDefault(),
        Prompt = form["prompt"].FirstOrDefault(),
        ResponseFormat = form["response_format"].FirstOrDefault(),
        Temperature = double.TryParse(form["temperature"].FirstOrDefault(), out var temp) ? temp : null
    };

    if (form.Files.GetFile("file") != null)
    {
        using var ms = new MemoryStream();
        await form.Files["file"].CopyToAsync(ms);
        request.File = ms.ToArray();
        request.FileName = form.Files["file"].FileName;
    }

    var response = await routingService.RouteAudioTranslationAsync(request, apiKey);

    var responseFormat = form["response_format"].FirstOrDefault() ?? "json";
    if (responseFormat == "text") return Results.Text(response.Text, "text/plain");

    return Results.Ok(response);
});

app.MapPost("/v1/audio/speech",
            async (AudioSpeechRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var audioBytes = await routingService.RouteAudioSpeechAsync(request, apiKey);

                if (audioBytes.Length == 0)
                    return Results.BadRequest(new
                    {
                        error = new { message = "Audio generation failed", type = "server_error" }
                    });

                var responseFormat = request.ResponseFormat ?? "mp3";
                var contentType = responseFormat switch
                {
                    "mp3" => "audio/mpeg",
                    "opus" => "audio/opus",
                    "aac" => "audio/aac",
                    "flac" => "audio/flac",
                    "wav" => "audio/wav",
                    "pcm" => "audio/pcm",
                    _ => "audio/mpeg"
                };

                context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"speech.{responseFormat}\"";
                return Results.File(audioBytes, contentType);
            });

// Batches API
app.MapPost("/v1/batches", async (BatchRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteBatchCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/batches/{batchId}", async (string batchId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteBatchRetrieveAsync(batchId, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/batches", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteBatchListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapPost("/v1/batches/{batchId}/cancel",
            async (string batchId, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteBatchCancelAsync(batchId, apiKey);
                return Results.Ok(response);
            });

app.MapPost("/v1/batches/{batchId}/cancel",
            async (string batchId, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteBatchCancelAsync(batchId, apiKey);
                return Results.Ok(response);
            });

// Fine-tuning API
app.MapPost("/v1/fine_tuning/jobs",
            async (FineTuningJobRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteFineTuningCreateAsync(request, apiKey);
                return Results.Ok(response);
            });

app.MapGet("/v1/fine_tuning/jobs", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteFineTuningListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/fine_tuning/jobs/{jobId}", async (string jobId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteFineTuningRetrieveAsync(jobId, apiKey);
    return Results.Ok(response);
});

app.MapPost("/v1/fine_tuning/jobs/{jobId}/cancel",
            async (string jobId, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteFineTuningCancelAsync(jobId, apiKey);
                return Results.Ok(response);
            });

app.MapGet("/v1/fine_tuning/jobs/{jobId}/events",
           async (string jobId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
               var after = context.Request.Query["after"].FirstOrDefault();

               var response = await routingService.RouteFineTuningEventsAsync(jobId, limit, after, apiKey);
               return Results.Ok(response);
           });

app.MapGet("/v1/fine_tuning/jobs/{jobId}/checkpoints",
           async (string jobId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
               var after = context.Request.Query["after"].FirstOrDefault();

               var response = await routingService.RouteFineTuningCheckpointsAsync(jobId, limit, after, apiKey);
               return Results.Ok(response);
           });

// Files API
app.MapPost("/v1/files", async (HttpContext context, IRoutingService routingService) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var form = await context.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var purpose = form["purpose"].FirstOrDefault() ?? "fine-tune";

    var request = new FileUploadRequest
    {
        File = file,
        Purpose = purpose
    };

    var response = await routingService.RouteFileUploadAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/files", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var purpose = context.Request.Query["purpose"].FirstOrDefault();
    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteFileListAsync(purpose, limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/files/{fileId}", async (string fileId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteFileRetrieveAsync(fileId, apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new { message = $"File '{fileId}' not found", type = "not_found_error", code = "file_not_found" }
        });

    return Results.Ok(response);
});

app.MapDelete("/v1/files/{fileId}", async (string fileId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var deleted = await routingService.RouteFileDeleteAsync(fileId, apiKey);
    if (!deleted)
        return Results.NotFound(new
        {
            error = new { message = $"File '{fileId}' not found", type = "not_found_error", code = "file_not_found" }
        });

    return Results.Ok(new { id = fileId, @object = "file", deleted = true });
});

app.MapGet("/v1/files/{fileId}/content", async (string fileId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var content = await routingService.RouteFileContentAsync(fileId, apiKey);
    if (content == null)
        return Results.NotFound(new
        {
            error = new { message = $"File '{fileId}' not found", type = "not_found_error", code = "file_not_found" }
        });

    // Get file info for content disposition
    var fileInfo = await routingService.RouteFileRetrieveAsync(fileId, apiKey);
    var fileName = fileInfo?.Filename ?? fileId;

    context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
    return Results.File(content, "application/octet-stream", fileName);
});

// Assistants API
// Assistants
app.MapPost("/v1/assistants", async (AssistantRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteAssistantCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/assistants", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteAssistantListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/assistants/{assistantId}",
           async (string assistantId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var response = await routingService.RouteAssistantRetrieveAsync(assistantId, apiKey);
               if (response == null)
                   return Results.NotFound(new
                   {
                       error = new
                       {
                           message = $"Assistant '{assistantId}' not found", type = "not_found_error",
                           code = "assistant_not_found"
                       }
                   });

               return Results.Ok(response);
           });

app.MapPost("/v1/assistants/{assistantId}",
            async (string assistantId, AssistantRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteAssistantUpdateAsync(assistantId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Assistant '{assistantId}' not found", type = "not_found_error",
                            code = "assistant_not_found"
                        }
                    });

                return Results.Ok(response);
            });

app.MapDelete("/v1/assistants/{assistantId}",
              async (string assistantId, IRoutingService routingService, HttpContext context) =>
              {
                  var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                  var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                  var deleted = await routingService.RouteAssistantDeleteAsync(assistantId, apiKey);
                  if (!deleted)
                      return Results.NotFound(new
                      {
                          error = new
                          {
                              message = $"Assistant '{assistantId}' not found", type = "not_found_error",
                              code = "assistant_not_found"
                          }
                      });

                  return Results.Ok(new { id = assistantId, @object = "assistant", deleted = true });
              });

// Threads
app.MapPost("/v1/threads", async (ThreadRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteThreadCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/threads/{threadId}", async (string threadId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteThreadRetrieveAsync(threadId, apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"Thread '{threadId}' not found", type = "not_found_error", code = "thread_not_found"
            }
        });

    return Results.Ok(response);
});

app.MapPost("/v1/threads/{threadId}",
            async (string threadId, ThreadRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteThreadUpdateAsync(threadId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Thread '{threadId}' not found", type = "not_found_error",
                            code = "thread_not_found"
                        }
                    });

                return Results.Ok(response);
            });

app.MapDelete("/v1/threads/{threadId}", async (string threadId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var deleted = await routingService.RouteThreadDeleteAsync(threadId, apiKey);
    if (!deleted)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"Thread '{threadId}' not found", type = "not_found_error", code = "thread_not_found"
            }
        });

    return Results.Ok(new { id = threadId, @object = "thread", deleted = true });
});

// Messages
app.MapPost("/v1/threads/{threadId}/messages",
            async (string threadId, MessageRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteMessageCreateAsync(threadId, request, apiKey);
                return Results.Ok(response);
            });

app.MapGet("/v1/threads/{threadId}/messages",
           async (string threadId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
               var after = context.Request.Query["after"].FirstOrDefault();
               var before = context.Request.Query["before"].FirstOrDefault();

               var response = await routingService.RouteMessageListAsync(threadId, limit, after, before, apiKey);
               return Results.Ok(response);
           });

app.MapGet("/v1/threads/{threadId}/messages/{messageId}",
           async (string threadId, string messageId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var response = await routingService.RouteMessageRetrieveAsync(threadId, messageId, apiKey);
               if (response == null)
                   return Results.NotFound(new
                   {
                       error = new
                       {
                           message = $"Message '{messageId}' not found", type = "not_found_error",
                           code = "message_not_found"
                       }
                   });

               return Results.Ok(response);
           });

app.MapPost("/v1/threads/{threadId}/messages/{messageId}",
            async (string threadId, string messageId, MessageRequest request, IRoutingService routingService,
                   HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteMessageUpdateAsync(threadId, messageId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Message '{messageId}' not found", type = "not_found_error",
                            code = "message_not_found"
                        }
                    });

                return Results.Ok(response);
            });

// Runs
app.MapPost("/v1/threads/{threadId}/runs",
            async (string threadId, RunRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteRunCreateAsync(threadId, request, apiKey);
                return Results.Ok(response);
            });

app.MapGet("/v1/threads/{threadId}/runs",
           async (string threadId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
               var after = context.Request.Query["after"].FirstOrDefault();

               var response = await routingService.RouteRunListAsync(threadId, limit, after, apiKey);
               return Results.Ok(response);
           });

app.MapGet("/v1/threads/{threadId}/runs/{runId}",
           async (string threadId, string runId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var response = await routingService.RouteRunRetrieveAsync(threadId, runId, apiKey);
               if (response == null)
                   return Results.NotFound(new
                   {
                       error = new
                       {
                           message = $"Run '{runId}' not found", type = "not_found_error", code = "run_not_found"
                       }
                   });

               return Results.Ok(response);
           });

app.MapPost("/v1/threads/{threadId}/runs/{runId}",
            async (string threadId, string runId, RunRequest request, IRoutingService routingService,
                   HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteRunUpdateAsync(threadId, runId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Run '{runId}' not found", type = "not_found_error", code = "run_not_found"
                        }
                    });

                return Results.Ok(response);
            });

app.MapPost("/v1/threads/{threadId}/runs/{runId}/cancel",
            async (string threadId, string runId, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteRunCancelAsync(threadId, runId, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Run '{runId}' not found", type = "not_found_error", code = "run_not_found"
                        }
                    });

                return Results.Ok(response);
            });

app.MapPost("/v1/threads/{threadId}/runs/{runId}/submit_tool_outputs",
            async (string threadId, string runId, SubmitToolOutputsRequest request, IRoutingService routingService,
                   HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteRunSubmitToolOutputsAsync(threadId, runId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Run '{runId}' not found", type = "not_found_error", code = "run_not_found"
                        }
                    });

                return Results.Ok(response);
            });

// Run Steps
app.MapGet("/v1/threads/{threadId}/runs/{runId}/steps",
           async (string threadId, string runId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
               var after = context.Request.Query["after"].FirstOrDefault();

               var response = await routingService.RouteRunStepListAsync(threadId, runId, limit, after, apiKey);
               return Results.Ok(response);
           });

app.MapGet("/v1/threads/{threadId}/runs/{runId}/steps/{stepId}",
           async (string threadId, string runId, string stepId, IRoutingService routingService, HttpContext context) =>
           {
               var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
               var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

               var response = await routingService.RouteRunStepRetrieveAsync(threadId, runId, stepId, apiKey);
               if (response == null)
                   return Results.NotFound(new
                   {
                       error = new
                       {
                           message = $"Step '{stepId}' not found", type = "not_found_error", code = "step_not_found"
                       }
                   });

               return Results.Ok(response);
           });

// Admin/Organization API (OpenAI-compatible)
// API Keys
app.MapPost("/v1/api_keys", async (ApiKeyRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteApiKeyCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/api_keys", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteApiKeyListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/api_keys/{keyId}", async (string keyId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteApiKeyRetrieveAsync(keyId, apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"API key '{keyId}' not found", type = "not_found_error", code = "api_key_not_found"
            }
        });

    return Results.Ok(response);
});

app.MapDelete("/v1/api_keys/{keyId}", async (string keyId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var deleted = await routingService.RouteApiKeyDeleteAsync(keyId, apiKey);
    if (!deleted)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"API key '{keyId}' not found", type = "not_found_error", code = "api_key_not_found"
            }
        });

    return Results.Ok(new { id = keyId, @object = "api_key", deleted = true });
});

// Projects
app.MapPost("/v1/projects", async (ProjectRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteProjectCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/projects", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteProjectListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/projects/{projectId}", async (string projectId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteProjectRetrieveAsync(projectId, apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"Project '{projectId}' not found", type = "not_found_error", code = "project_not_found"
            }
        });

    return Results.Ok(response);
});

app.MapPost("/v1/projects/{projectId}",
            async (string projectId, ProjectRequest request, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteProjectUpdateAsync(projectId, request, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Project '{projectId}' not found", type = "not_found_error",
                            code = "project_not_found"
                        }
                    });

                return Results.Ok(response);
            });

app.MapPost("/v1/projects/{projectId}/archive",
            async (string projectId, IRoutingService routingService, HttpContext context) =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

                var response = await routingService.RouteProjectArchiveAsync(projectId, apiKey);
                if (response == null)
                    return Results.NotFound(new
                    {
                        error = new
                        {
                            message = $"Project '{projectId}' not found", type = "not_found_error",
                            code = "project_not_found"
                        }
                    });

                return Results.Ok(response);
            });

// Users
app.MapGet("/v1/users", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteUserListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/users/{userId}", async (string userId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteUserRetrieveAsync(userId, apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new { message = $"User '{userId}' not found", type = "not_found_error", code = "user_not_found" }
        });

    return Results.Ok(response);
});

// Invites
app.MapPost("/v1/invites", async (InviteRequest request, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteInviteCreateAsync(request, apiKey);
    return Results.Ok(response);
});

app.MapGet("/v1/invites", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteInviteListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

app.MapDelete("/v1/invites/{inviteId}", async (string inviteId, IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var deleted = await routingService.RouteInviteDeleteAsync(inviteId, apiKey);
    if (!deleted)
        return Results.NotFound(new
        {
            error = new
            {
                message = $"Invite '{inviteId}' not found", type = "not_found_error", code = "invite_not_found"
            }
        });

    return Results.Ok(new { id = inviteId, @object = "invite", deleted = true });
});

// Audit Logs
app.MapGet("/v1/audit_logs", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var l) ? (int?)l : null;
    var after = context.Request.Query["after"].FirstOrDefault();

    var response = await routingService.RouteAuditLogListAsync(limit, after, apiKey);
    return Results.Ok(response);
});

// Organization
app.MapGet("/v1/organization", async (IRoutingService routingService, HttpContext context) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var apiKey = authHeader?.Replace("Bearer ", "") ?? "";

    var response = await routingService.RouteOrganizationRetrieveAsync(apiKey);
    if (response == null)
        return Results.NotFound(new
        {
            error = new
            {
                message = "Organization not found", type = "not_found_error", code = "organization_not_found"
            }
        });

    return Results.Ok(response);
});

// Admin endpoints for managing providers (internal)
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