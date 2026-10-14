# VanPatten AI Router

[Русский](README-RU.md) | **English**

A lightweight, OpenAI-compatible API gateway for routing requests (like openrouter.ai) to multiple LLM providers with intelligent failover, rate limiting, and virtual model support.

## Features

- **OpenAI-Compatible API** — Drop-in replacement for OpenAI API (`/v1/chat/completions`, `/v1/models`, `/v1/embeddings`)
- **Multi-Provider Support** — Route to KiloCode, OpenAI, Anthropic, Ollama, and custom OpenAI-compatible endpoints
- **Virtual Models** — `vanpatten/auto-free` automatically routes to free models across providers; `vanpatten/auto` routes to all models
- **Intelligent Failover** — Automatic retry with exponential backoff across providers based on priority
- **Rate Limiting** — Per-provider rate limits (requests/day, tokens/hour) with automatic enforcement
- **Provider Priority** — Configurable priority system for provider selection order
- **Model Refresh** — Automatic background model discovery from provider `/models` endpoints
- **Admin API** — REST endpoints for managing providers, viewing stats, and triggering model refresh
- **SQLite Persistence** — Embedded database for provider state, rate limits, and model catalog
- **Hot Config Reload** — Changes to `appsettings.json` picked up automatically without restart

## Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│   Client App    │────▶│  VanPatten AI    │────▶│  KiloCode        │
│  (OpenAI SDK)   │     │  Router          │     │  Provider 1      │
└─────────────────┘     │                  │     │  (Priority 110)  │
                        │  - Routing       │     └──────────────────┘
                        │  - Failover      │     ┌──────────────────┐
                        │  - Rate Limit    │────▶│  KiloCode        │
                        │  - Virtual Models│     │  Provider 2      │
                        │  - Model Cache   │     │  (Priority 108)  │
                        └──────────────────┘     └──────────────────┘
                                  |                   ...
                        ┌──────────────────┐     ┌──────────────────┐
                        │  SQLite DB       │     │  OpenAI /        │
                        │  - Providers     │     │  Anthropic /     │
                        │  - Models        │     │  Ollama          │
                        │  - Rate Limits   │     │  (Lower Priority │
                        └──────────────────┘     └──────────────────┘
```

## Quick Start

### Prerequisites

- Docker & Docker Compose
- Or .NET~~~~ 10 SDK for local development

### Using Docker (Recommended)

1. **Clone and configure:**
   ```bash
   git clone <repo-url>
   cd VanPattenAIRouter
   cp src/appsettings.json src/appsettings.Development.json  # optional, for local overrides
   ```

2. **Edit `src/appsettings.json`** with your provider API keys:
   ```json
   {
     "Router": {
       "Providers": [
         {
           "Name": "KiloCode - your-email@example.com",
           "Type": "kilocode",
           "BaseUrl": "https://api.kilo-code.com",
           "ApiKey": "your-kilocode-api-key",
           "Priority": 100,
           "IsActive": true,
           "RateLimit": "RequestsPerDay": 0,
                        "TokensPerDay": 0,
                        "RequestsPerHour": 200,
                        "TokensPerHour": 500000
         }
       ]
     }
   }
   ```

3. **Start the service:**
   ```bash
   docker-compose up -d
   ```

4. **Verify it's running:**
   ```bash
   curl http://localhost:10080/v1/models
   ```

### Local Development

```bash
cd src
dotnet restore
dotnet run
# API available at http://localhost:8080
```

## Configuration

All configuration is in `src/appsettings.json`. The file is mounted as a volume in Docker, so changes are picked up automatically (hot reload).

### Router Section

```json
"Router": {
  "DefaultProvider": "VanPatten",
  "TimeoutSeconds": 120,
  "ModelRefreshIntervalMinutes": 10,
  "FreeModels": {
    "KiloCode": ["model-id-1", "model-id-2"],
    "NVIDIA": ["nvidia/nemotron-3-ultra:free"],
    "OpenRouter": [":free", "-free"]
  },
  "Retry": {
    "MaxAttempts": 3,
    "BaseDelayMs": 500,
    "MaxDelayMs": 5000,
    "BackoffMultiplier": 2.0,
    "RetryableStatusCodes": [429, 500, 502, 503, 504]
  },
  "Providers": [
    {
      "Name": "KiloCode - user@example.com",
      "Type": "kilocode",
      "BaseUrl": "https://api.kilo-code.com",
      "ApiKey": "sk-xxxxx",
      "Priority": 110,
      "IsActive": true,
      "RateLimit": {
        "MaxRequestsPerDay": 200,
        "MaxTokensPerHour": 500000
      }
    }
  ]
}
```

### Key Settings

| Setting | Description |
|---------|-------------|
| `DefaultProvider` | Provider used for non-virtual model requests |
| `TimeoutSeconds` | Request timeout for provider calls |
| `ModelRefreshIntervalMinutes` | How often to refresh model catalog from providers |
| `FreeModels` | Dictionary of provider prefix → list of free model patterns |
| `Retry` | Global retry policy for failed requests |
| `Providers[]` | Array of provider configurations |

### Provider Types

| Type | Description |
|------|-------------|
| `kilocode` | KiloCode API (OpenAI-compatible) |
| `openai` | Official OpenAI API |
| `anthropic` | Anthropic API (via OpenAI-compatible wrapper) |
| `ollama` | Local Ollama instance |
| `openai_compatible` | Any OpenAI-compatible endpoint |

### Database

```json
"Database": {
  "Path": "data/vanpatten-airrouter.db",
  "RecreateOnStartup": false
}
```

- `Path` — SQLite database file path (relative to app working directory)
- `RecreateOnStartup` — Set to `true` for one-time DB recreation on next restart (useful after config changes), then set back to `false`

## API Endpoints

### OpenAI-Compatible Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/chat/completions` | Chat completions (streaming supported) |
| `GET` | `/v1/models` | List all available models |
| `POST` | `/v1/embeddings` | Create embeddings |

### Admin Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/admin/providers` | List all providers with status |
| `GET` | `/admin/providers/{id}` | Get provider details |
| `POST` | `/admin/providers` | Create new provider |
| `PUT` | `/admin/providers/{id}` | Update provider |
| `DELETE` | `/admin/providers/{id}` | Delete provider |
| `POST` | `/admin/providers/{id}/refresh-models` | Trigger model refresh for provider |
| `POST` | `/admin/refresh-all-models` | Refresh models for all providers |
| `GET` | `/admin/stats` | Get routing statistics |
| `GET` | `/admin/health` | Health check |

### Virtual Models

The virtual provider `VanPatten` supports three routing modes:

| Model ID | Mode | Description |
|----------|------|-------------|
| `vanpatten/auto-free` | `auto-free` | Routes to **free** models only across all providers |
| `vanpatten/auto` | `auto` | Routes to **all** models (free + paid) across all providers |
| `vanpatten/{model-name}` | `specific` | Routes to a **specific model** with failover to other providers that have the same model |

#### Mode Details

**1. `auto-free` (e.g., `vanpatten/auto-free`)**
- Only considers models marked as free
- Tries providers in priority order
- Falls through to next provider on rate limit or error

**2. `auto` (e.g., `vanpatten/auto`)**
- Considers all models (free and paid)
- Tries providers in priority order
- Falls through to next provider on rate limit or error

**3. Specific Model (e.g., `vanpatten/google/gemini-2.0-flash-exp:free`)**
- User specifies exact model name: `vanpatten/{model-id}`
- Router finds all providers that have this exact model (must be free and enabled)
- Tries providers in priority order
- If a provider hits rate limit or fails, **automatically fails over to the next provider that has the same model**
- Only falls back to other models if no provider has the requested model

#### How Virtual Routing Works

When using any virtual model, the router:
1. Resolves the virtual provider and mode from the model name
2. Finds candidate models based on mode:
   - `auto-free`: All free models from active providers
   - `auto`: All models from active providers
   - `specific`: Only the requested model from providers that have it
3. Groups by provider, sorts by provider priority (highest first)
4. Iterates providers in priority order, trying models until one succeeds
5. Skips rate-limited providers automatically
6. Returns first successful response with actual provider/model used in response

### Usage Examples

```bash
# Auto-free: only free models
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto-free", "messages": [{"role": "user", "content": "Hi"}]}'

# Auto: all models (free + paid)
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto", "messages": [{"role": "user", "content": "Hi"}]}'

# Specific model with failover: tries this exact model on each provider
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/google/gemini-2.0-flash-exp:free", "messages": [{"role": "user", "content": "Hi"}]}'
```

## Provider Priority System

Providers are sorted by `Priority` (higher = tried first). Current defaults:

```
VanPatten (virtual)          → 200  (routes to real providers)
KiloCode - primary           → 110
KiloCode - secondary         → 108
KiloCode - tertiary          → 106
KiloCode - quaternary        → 104
KiloCode - quinary           → 102
OpenAI                       → 90   (inactive by default)
Anthropic                    → 80   (inactive by default)
Local Ollama                 → 50   (inactive by default)
```

All KiloCode accounts have higher priority than paid providers. Adjust based on your preferences.

## Rate Limiting

Each provider can have rate limits:

```json
"RateLimit": {
  "MaxRequestsPerDay": 200,
  "MaxTokensPerHour": 500000
}
```

The router tracks usage per provider and will skip rate-limited providers during routing.

## Model Refresh

Models are automatically discovered from provider `/models` endpoints every `ModelRefreshIntervalMinutes` (default: 10). You can also trigger manually:

```bash
curl -X POST http://localhost:10080/admin/refresh-all-models
```

Free/paid classification for KiloCode models uses the `FreeModels.KiloCode` list in config. For other providers, heuristic patterns (`:free`, `-free`, `/free`, `free-`) are used.

## Testing

```bash
# Run all tests
cd src
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

```
VanPattenAIRouter/
├── docker-compose.yml          # Docker orchestration
├── Dockerfile                  # Multi-stage build
├── README.md                   # This file
├── README-RU.md                # Russian version
├── src/
│   ├── VanPattenAIRouter.csproj
│   ├── Program.cs              # Entry point, DI, middleware
│   ├── appsettings.json        # Configuration (mounted in Docker)
│   ├── Controllers/
│   │   ├── ChatController.cs   # /v1/chat/completions
│   │   ├── ModelsController.cs # /v1/models
│   │   ├── EmbeddingsController.cs
│   │   └── AdminController.cs  # /admin/*
│   ├── Services/
│   │   ├── RoutingService.cs   # Core routing & failover logic
│   │   ├── ProviderService.cs  # Provider CRUD & queries
│   │   ├── ModelRefreshService.cs # Background model discovery
│   │   └── RetryPolicyService.cs  # Retry with exponential backoff
│   ├── Data/
│   │   ├── AppDbContext.cs     # EF Core SQLite context
│   │   ├── SeedData.cs         # Initial provider/model seeding
│   │   └── Migrations/         # EF Core migrations
│   ├── Models/
│   │   ├── Provider.cs         # Provider entity
│   │   ├── ProviderModel.cs    # Model entity
│   │   ├── RouterOptions.cs    # Typed configuration classes
│   │   └── ProviderDto.cs      # DTOs for API
│   └── Middleware/
│       └── ErrorHandlingMiddleware.cs
└── tests/
    └── VanPattenAIRouter.Tests/
        ├── ModelRefreshServiceTests.cs
        ├── RoutingServiceTests.cs
        └── ProviderServiceTests.cs
```

## Docker Details

### Dockerfile (Multi-stage)

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/VanPattenAIRouter.csproj", "."]
RUN dotnet restore
COPY src/. .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VanPattenAIRouter.dll"]
```

### Volumes

| Host Path | Container Path | Purpose |
|-----------|----------------|---------|
| `./data` | `/app/data` | SQLite database persistence |
| `./src/appsettings.json` | `/app/appsettings.json` | Config (read-only, hot reload) |

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |
| `ASPNETCORE_URLS` | URLs to listen on | `http://+:8080` |

## Troubleshooting

### Models not appearing
- Check provider `IsActive: true` in config
- Check API key validity
- Trigger manual refresh: `POST /admin/refresh-all-models`
- Check logs: `docker-compose logs -f`

### Requests failing with 500
- Check provider rate limits in `/admin/stats`
- Verify provider `BaseUrl` is accessible from container
- Check container logs for upstream error details

### Database issues
- Set `"RecreateOnStartup": true` in `appsettings.json`, restart, then set back to `false`
- Or delete `data/vanpatten-airrouter.db` and restart

### Config not reloading
- Ensure `appsettings.json` is mounted as volume (read-only is fine)
- File must be modified on host (not in container) for reload to trigger

## Security Considerations

- **API keys in config** — Store `appsettings.json` securely; consider using Docker secrets or Azure Key Vault in production
- **No authentication** — The router itself has no auth; place behind a reverse proxy with auth in production
- **Rate limits** — Configure per-provider limits to avoid unexpected costs
- **Network** — Run in isolated network; don't expose directly to internet

## Production Deployment

1. **Reverse Proxy** — Use nginx/Traefik for TLS termination and auth
2. **Secrets** — Move API keys to Docker secrets / Azure Key Vault / AWS Secrets Manager
3. **Monitoring** — Add health checks (`/admin/health`), logging, metrics
4. **Scaling** — Single instance is sufficient for most use cases; SQLite doesn't support concurrent writers
5. **Backups** — Backup `data/vanpatten-airrouter.db` regularly

## License

MIT License — see LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes with tests
4. Submit a PR

## Support

For issues and feature requests, please open a GitHub issue.