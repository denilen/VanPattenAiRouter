# VanPatten AI Router

[Русский](README-RU.md) | **English**

A lightweight, **100% OpenAI-compatible** API gateway for routing requests to multiple LLM providers with intelligent failover, rate limiting, virtual models, and full OpenAI API parity.

## 🚀 Features

- **100% OpenAI API Compatible** — Drop-in replacement for all OpenAI endpoints
- **Multi-Provider Support** — Route to KiloCode, OpenAI, Anthropic, Ollama, and custom OpenAI-compatible endpoints
- **Virtual Models** — `vanpatten/auto-free` (free models), `vanpatten/auto` (all models), `vanpatten/{model}` (specific model with failover)
- **Intelligent Failover** — Automatic retry with exponential backoff across providers based on priority
- **Rate Limiting** — Per-provider rate limits (requests/day, tokens/hour) with automatic enforcement
- **Provider Priority** — Configurable priority system for provider selection order
- **Model Refresh** — Automatic background model discovery from provider `/models` endpoints
- **Full OpenAI API Coverage** — Chat, Models, Embeddings, Completions, Moderations, Images, Audio, Batches, Assistants, Fine-tuning, Files, Admin
- **SQLite Persistence** — Embedded database for provider state, rate limits, model catalog, assistants, threads, runs, files
- **Hot Config Reload** — Changes to `appsettings.json` picked up automatically without restart
- **Production Ready** — Clean error handling, structured logging, OpenAI-compatible headers, stress-tested streaming

## 📋 Complete OpenAI API Coverage

| Category | Endpoints | Status |
|----------|-----------|--------|
| **Chat Completions** | `POST /v1/chat/completions` (stream + non-stream) | ✅ **Complete** |
| **Models** | `GET /v1/models`, `GET /v1/models?grouped=true`, `GET /v1/models/{id}` | ✅ **Complete** |
| **Embeddings** | `POST /v1/embeddings` | ✅ **Complete** |
| **Legacy Completions** | `POST /v1/completions` (stream + non-stream) | ✅ **Complete** |
| **Moderations** | `POST /v1/moderations` | ✅ **Complete** |
| **Images (DALL-E)** | `POST /v1/images/generations`, `/edits`, `/variations` | ✅ **Complete** |
| **Audio (Whisper/TTS)** | `POST /v1/audio/transcriptions`, `/translations`, `/speech` | ✅ **Complete** |
| **Batches** | `POST /v1/batches`, `GET /v1/batches`, `GET /v1/batches/{id}`, `POST /v1/batches/{id}/cancel` | ✅ **Complete** |
| **Assistants** | 20+ endpoints (assistants, threads, messages, runs, run steps) | ✅ **Complete** |
| **Fine-tuning** | `POST/GET /v1/fine_tuning/jobs`, events, checkpoints | ✅ **Complete** |
| **Files** | `POST/GET/DELETE /v1/files`, `GET /v1/files/{id}/content` | ✅ **Complete** |
| **Admin/Organization** | API Keys, Projects, Users, Invites, Audit Logs, Organization | ✅ **Complete** |

## 🏗️ Architecture

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
                        │  - Assistants    │     └──────────────────┘
                        │  - Threads       │
                        │  - Files         │
                        └──────────────────┘
```

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- Or .NET 10 SDK for local development

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
        "RateLimit": {
          "RequestsPerDay": 0,
          "TokensPerDay": 0,
          "RequestsPerHour": 200,
          "TokensPerHour": 500000
        }
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

### Enable Full OpenAI Backend (for Images, Audio, Fine-tuning, Assistants, Batches)
```bash
export OPENAI_API_KEY=sk-your-openai-key
docker-compose down && docker-compose up -d --build
```

## ⚙️ Configuration

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
    "RetryableStatusCodes": [402, 429, 500, 502, 503, 504]
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
- `RecreateOnStartup` — Set to `true` for one-time DB recreation on next restart, then set back to `false`

## 📚 API Endpoints

### OpenAI-Compatible Endpoints

#### Chat Completions
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/chat/completions` | Chat completions with full OpenAI parameter support (streaming + non-stream) |

**Supported Parameters:** `model`, `messages`, `max_tokens`/`max_completion_tokens`, `temperature`, `top_p`, `n`, `stream`, `stop`, `presence_penalty`, `frequency_penalty`, `logit_bias`, `user`, `seed`, `tools`/`functions`, `tool_choice`, `parallel_tool_calls`, `response_format`, `logprobs`, `top_logprobs`, `prediction`, `store`, `metadata`, `stream_options` (`include_usage`, `include_obfuscation`), `reasoning_effort`, `service_tier`, `prompt_cache_key`, `verbosity`, `audio`, `modalities`, `web_search_options`, `function_call` (deprecated), `functions` (deprecated)

**Streaming Features:** `stream_options.include_usage` (final usage chunk), `stream_options.include_obfuscation`, SSE format matching OpenAI exactly

#### Models
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/models` | List all available models |
| `GET` | `/v1/models?grouped=true` | Grouped by free/provider |
| `GET` | `/v1/models/{id}` | Retrieve specific model |

#### Embeddings
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/embeddings` | Create embeddings (supports `encoding_format`, `dimensions`, `user`, batch input) |

#### Legacy Completions
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/completions` | Text completions (streaming supported) |

#### Moderations
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/moderations` | Content moderation (batch input, 10 categories) |

#### Images (DALL-E)
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/images/generations` | Generate images |
| `POST` | `/v1/images/edits` | Edit images (multipart) |
| `POST` | `/v1/images/variations` | Create variations (multipart) |

#### Audio (Whisper/TTS)
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/audio/transcriptions` | Transcribe audio (multipart) |
| `POST` | `/v1/audio/translations` | Translate audio to English (multipart) |
| `POST` | `/v1/audio/speech` | Text-to-speech (returns audio binary) |

#### Batches
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/batches` | Create batch |
| `GET` | `/v1/batches` | List batches (pagination) |
| `GET` | `/v1/batches/{id}` | Retrieve batch |
| `POST` | `/v1/batches/{id}/cancel` | Cancel batch |

#### Assistants (20+ endpoints)
| Method | Path | Description |
|--------|------|-------------|
| `POST/GET` | `/v1/assistants` | Create/list assistants |
| `GET/POST/DELETE` | `/v1/assistants/{id}` | Retrieve/update/delete assistant |
| `POST/GET/DELETE` | `/v1/threads` | Create/list/delete threads |
| `GET/POST` | `/v1/threads/{id}` | Retrieve/update thread |
| `POST/GET` | `/v1/threads/{thread_id}/messages` | Create/list messages |
| `GET/POST` | `/v1/threads/{thread_id}/messages/{id}` | Retrieve/update message |
| `POST/GET` | `/v1/threads/{thread_id}/runs` | Create/list runs |
| `GET/POST` | `/v1/threads/{thread_id}/runs/{id}` | Retrieve/update/cancel run |
| `POST` | `/v1/threads/{thread_id}/runs/{id}/submit_tool_outputs` | Submit tool outputs |
| `GET` | `/v1/threads/{thread_id}/runs/{id}/steps` | List run steps |

**Features:** Tools (code_interpreter, file_search, functions), file_ids, temperature, top_p, truncation_strategy, tool_choice, response_format, background run processing

#### Fine-tuning
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/fine_tuning/jobs` | Create fine-tuning job |
| `GET` | `/v1/fine_tuning/jobs` | List jobs |
| `GET/POST` | `/v1/fine_tuning/jobs/{id}` | Retrieve/cancel job |
| `GET` | `/v1/fine_tuning/jobs/{id}/events` | List job events |
| `GET` | `/v1/fine_tuning/jobs/{id}/checkpoints` | List checkpoints |

#### Files
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/files` | Upload file (multipart, purposes: fine-tune, assistants, batch, etc.) |
| `GET` | `/v1/files` | List files (pagination, filter by purpose) |
| `GET` | `/v1/files/{id}` | Retrieve file info |
| `DELETE` | `/v1/files/{id}` | Delete file |
| `GET` | `/v1/files/{id}/content` | Download file content |

#### Admin/Organization
| Method | Path | Description |
|--------|------|-------------|
| `POST/GET/DELETE` | `/v1/api_keys` | Manage API keys |
| `POST/GET/POST/POST` | `/v1/projects` | Create/list/update/archive projects |
| `GET` | `/v1/users` | List users |
| `POST/GET/DELETE` | `/v1/invites` | Manage invites |
| `GET` | `/v1/audit_logs` | List audit logs |
| `GET` | `/v1/organization` | Retrieve organization |

#### Admin Endpoints (Internal)
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/admin/providers` | List all providers with status |
| `POST/PUT/DELETE` | `/admin/providers` | CRUD providers |
| `POST` | `/admin/providers/{id}/refresh-models` | Trigger model refresh |
| `POST` | `/admin/refresh-all-models` | Refresh all models |
| `GET` | `/admin/stats` | Routing statistics |
| `GET` | `/admin/health` | Health check |

## 🎭 Virtual Models

The virtual provider `VanPatten` supports three routing modes:

| Model ID | Mode | Description |
|----------|------|-------------|
| `vanpatten/auto-free` | `auto-free` | Routes to **free** models only across all providers |
| `vanpatten/auto` | `auto` | Routes to **all** models (free + paid) across all providers |
| `vanpatten/{model-name}` | `specific` | Routes to a **specific model** with failover to other providers with same model |

### Usage Examples
```bash
# Auto-free: only free models
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto-free", "messages": [{"role": "user", "content": "Hi"}]}'

# Auto: all models (free + paid)
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto", "messages": [{"role": "user", "content": "Hi"}]}'

# Specific model with failover
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/google/gemini-2.0-flash-exp:free", "messages": [{"role": "user", "content": "Hi"}]}'
```

## 🔢 Provider Priority System

Providers sorted by `Priority` (higher = tried first):
```
VanPatten (virtual)          → 200
KiloCode - primary           → 110
KiloCode - secondary         → 108
KiloCode - tertiary          → 106
KiloCode - quaternary        → 104
KiloCode - quinary           → 102
OpenAI                       → 90   (inactive by default)
Anthropic                    → 80   (inactive by default)
Local Ollama                 → 50   (inactive by default)
```

## 📊 Rate Limiting
```json
"RateLimit": {
  "RequestsPerDay": 200,
  "TokensPerDay": 100000,
  "RequestsPerHour": 50,
  "TokensPerHour": 50000
}
```
Router tracks usage per provider and skips rate-limited providers during routing.

## 🔄 Model Refresh
Models auto-discovered from provider `/models` endpoints every `ModelRefreshIntervalMinutes` (default: 10).
Manual trigger: `POST /admin/refresh-all-models`

## 🧪 Testing
```bash
# Run all tests
cd src
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Stress test streaming
cd tests/VanPattenAIRouter.Tests/LLM-result
python3 test_stream_stability.py
```

## 📁 Project Structure
```
VanPattenAIRouter/
├── docker-compose.yml
├── Dockerfile
├── README.md
├── README-RU.md
├── src/
│   ├── VanPattenAIRouter.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Services/
│   │   ├── RoutingService.cs       # Core routing & failover
│   │   ├── ProviderService.cs      # Provider CRUD & queries
│   │   ├── ModelRefreshService.cs  # Background model discovery
│   │   └── RetryPolicyService.cs   # Retry with exponential backoff
│   ├── Data/
│   │   ├── AiRouterDbContext.cs    # EF Core SQLite context
│   │   └── SeedData.cs
│   ├── Models/
│   │   ├── Provider.cs
│   │   ├── ProviderModel.cs
│   │   ├── RouterOptions.cs
│   │   └── *.cs (all OpenAI models)
│   └── Middleware/
│       └── ErrorHandlingMiddleware.cs
└── tests/
    └── VanPattenAIRouter.Tests/
        ├── Unit/
        └── Integration/
```

## 🐳 Docker Details

### Dockerfile (Multi-stage)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/VanPattenAIRouter.csproj", "."]
RUN dotnet restore
COPY src/. .
RUN dotnet publish -c Release -o /app/publish

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

## 🔧 Environment Variables
| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |
| `ASPNETCORE_URLS` | URLs to listen on | `http://+:8080` |
| `OPENAI_API_KEY` | OpenAI API key (for full features) | — |

## 🛠️ Troubleshooting

### Models not appearing
- Check provider `IsActive: true` in config
- Check API key validity
- Trigger manual refresh: `POST /admin/refresh-all-models`
- Check logs: `docker-compose logs -f`

### Requests failing with 500
- Check provider rate limits in `/admin/stats`
- Verify provider `BaseUrl` accessible from container
- Check container logs for upstream error details

### Database issues
- Set `"RecreateOnStartup": true` in `appsettings.json`, restart, then set back to `false`
- Or delete `data/vanpatten-airrouter.db` and restart

### Config not reloading
- Ensure `appsettings.json` mounted as volume (read-only OK)
- File must be modified on host (not in container) for reload

## 🔒 Security Considerations
- **API keys in config** — Store securely; use Docker secrets / Azure Key Vault in production
- **No authentication** — Router has no auth; place behind reverse proxy with auth in production
- **Rate limits** — Configure per-provider limits to avoid unexpected costs
- **Network** — Run in isolated network; don't expose directly to internet

## 🚀 Production Deployment
1. **Reverse Proxy** — nginx/Traefik for TLS termination and auth
2. **Secrets** — Move API keys to Docker secrets / Azure Key Vault / AWS Secrets Manager
3. **Monitoring** — Health checks (`/admin/health`), logging, metrics
4. **Scaling** — Single instance sufficient; SQLite doesn't support concurrent writers
5. **Backups** — Backup `data/vanpatten-airrouter.db` regularly

## 📄 License
MIT License — see LICENSE file for details.

## 🤝 Contributing
1. Fork the repository
2. Create a feature branch
3. Make changes with tests
4. Submit a PR

## 📞 Support
For issues and feature requests, please open a GitHub issue.