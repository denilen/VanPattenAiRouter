# VanPatten AI Router

**Русский** | [English](README.md)

Лёгкий, **на 100% совместимый с OpenAI** API-шлюз для маршрутизации запросов к множественным LLM-провайдерам с интеллектуальным фейловером, ограничением частоты запросов, виртуальными моделями и полной совместимостью с OpenAI API.

## 🚀 Возможности

- **100% совместимость с OpenAI API** — Полная замена для всех эндпоинтов OpenAI
- **Поддержка множественных провайдеров** — Маршрутизация к KiloCode, OpenAI, Anthropic, Ollama и кастомным OpenAI-совместимым эндпоинтам
- **Виртуальные модели** — `vanpatten/auto-free` (бесплатные модели), `vanpatten/auto` (все модели), `vanpatten/{model}` (конкретная модель с фейловером)
- **Интеллектуальный фейловер** — Автоматические повторы с экспоненциальным бэкоффом между провайдерами на основе приоритета
- **Ограничение частоты запросов (Rate Limiting)** — Лимиты на провайдера (запросов в день/час, токенов в день/час) с автоматическим принудительным переключением
- **Приоритет провайдеров** — Настраиваемая система приоритетов для порядка выбора провайдеров
- **Обновление моделей** — Автоматическое фоновое обнаружение моделей с эндпоинтов `/models` провайдеров
- **Полное покрытие OpenAI API** — Chat, Models, Embeddings, Completions, Moderations, Images, Audio, Batches, Assistants, Fine-tuning, Files, Admin
- **SQLite Persistence** — Встроенная база данных для состояния провайдеров, rate limits, каталога моделей, ассистентов, тредов, запусков, файлов
- **Hot Config Reload** — Изменения в `appsettings.json` подхватываются автоматически без перезапуска
- **Production Ready** — Чистая обработка ошибок, структурированное логирование, OpenAI-совместимые заголовки, стресс-тестированный стриминг

## 📋 Полное покрытие OpenAI API

| Категория | Эндпоинты | Статус |
|-----------|-----------|--------|
| **Chat Completions** | `POST /v1/chat/completions` (stream + non-stream) | ✅ **Завершён** |
| **Models** | `GET /v1/models`, `GET /v1/models?grouped=true`, `GET /v1/models/{id}` | ✅ **Завершён** |
| **Embeddings** | `POST /v1/embeddings` | ✅ **Завершён** |
| **Legacy Completions** | `POST /v1/completions` (stream + non-stream) | ✅ **Завершён** |
| **Moderations** | `POST /v1/moderations` | ✅ **Завершён** |
| **Images (DALL-E)** | `POST /v1/images/generations`, `/edits`, `/variations` | ✅ **Завершён** |
| **Audio (Whisper/TTS)** | `POST /v1/audio/transcriptions`, `/translations`, `/speech` | ✅ **Завершён** |
| **Batches** | `POST /v1/batches`, `GET /v1/batches`, `GET /v1/batches/{id}`, `POST /v1/batches/{id}/cancel` | ✅ **Завершён** |
| **Assistants** | 20+ эндпоинтов (assistants, threads, messages, runs, run steps) | ✅ **Завершён** |
| **Fine-tuning** | `POST/GET /v1/fine_tuning/jobs`, events, checkpoints | ✅ **Завершён** |
| **Files** | `POST/GET/DELETE /v1/files`, `GET /v1/files/{id}/content` | ✅ **Завершён** |
| **Admin/Organization** | API Keys, Projects, Users, Invites, Audit Logs, Organization | ✅ **Завершён** |

## 🏗️ Архитектура

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

## 🚀 Быстрый старт

### Требования
- Docker & Docker Compose
- Или .NET 10 SDK для локальной разработки

### Использование Docker (Рекомендуется)

1. **Клонирование и настройка:**
```bash
git clone <repo-url>
cd VanPattenAIRouter
cp src/appsettings.json src/appsettings.Development.json  # опционально, для локальных переопределений
```

2. **Отредактируйте `src/appsettings.json`** с вашими API ключами провайдеров:
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

3. **Запустите сервис:**
```bash
docker-compose up -d
```

4. **Проверьте работу:**
```bash
curl http://localhost:10080/v1/models
```

### Локальная разработка
```bash
cd src
dotnet restore
dotnet run
# API доступен на http://localhost:8080
```

### Включение полного OpenAI бэкенда (для Images, Audio, Fine-tuning, Assistants, Batches)
```bash
export OPENAI_API_KEY=sk-ваш-openai-ключ
docker-compose down && docker-compose up -d --build
```

## ⚙️ Конфигурация

Вся конфигурация находится в `src/appsettings.json`. Файл монтируется как volume в Docker, поэтому изменения подхватываются автоматически (hot reload).

### Секция Router
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

### Ключевые настройки
| Настройка | Описание |
|-----------|----------|
| `DefaultProvider` | Провайдер, используемый для невиртуальных запросов к моделям |
| `TimeoutSeconds` | Таймаут запроса к провайдерам |
| `ModelRefreshIntervalMinutes` | Как часто обновлять каталог моделей от провайдеров |
| `FreeModels` | Словарь: префикс провайдера → список паттернов бесплатных моделей |
| `Retry` | Глобальная политика повторов для неудачных запросов |
| `Providers[]` | Массив конфигураций провайдеров |

### Типы провайдеров
| Тип | Описание |
|------|----------|
| `kilocode` | KiloCode API (OpenAI-совместимый) |
| `openai` | Официальный OpenAI API |
| `anthropic` | Anthropic API (через OpenAI-совместимую обёртку) |
| `ollama` | Локальный экземпляр Ollama |
| `openai_compatible` | Любой OpenAI-совместимый эндпоинт |

### База данных
```json
"Database": {
  "Path": "data/vanpatten-airrouter.db",
  "RecreateOnStartup": false
}
```
- `Path` — путь к файлу SQLite базы данных (относительно рабочей директории приложения)
- `RecreateOnStartup` — установите `true` для однократного пересоздания БД при следующем рестарте, затем верните `false`

## 📚 API Эндпоинты

### OpenAI-совместимые эндпоинты

#### Chat Completions
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/chat/completions` | Чат-комплишены с полной поддержкой параметров OpenAI (стриминг + non-stream) |

**Поддерживаемые параметры:** `model`, `messages`, `max_tokens`/`max_completion_tokens`, `temperature`, `top_p`, `n`, `stream`, `stop`, `presence_penalty`, `frequency_penalty`, `logit_bias`, `user`, `seed`, `tools`/`functions`, `tool_choice`, `parallel_tool_calls`, `response_format`, `logprobs`, `top_logprobs`, `prediction`, `store`, `metadata`, `stream_options` (`include_usage`, `include_obfuscation`), `reasoning_effort`, `service_tier`, `prompt_cache_key`, `verbosity`, `audio`, `modalities`, `web_search_options`, `function_call` (deprecated), `functions` (deprecated)

**Функции стриминга:** `stream_options.include_usage` (финальный usage-чанк), `stream_options.include_obfuscation`, SSE формат, точно соответствующий OpenAI

#### Models
| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/v1/models` | Список всех доступных моделей |
| `GET` | `/v1/models?grouped=true` | Группировка по free/provider |
| `GET` | `/v1/models/{id}` | Получение конкретной модели |

#### Embeddings
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/embeddings` | Создание эмбеддингов (поддерживает `encoding_format`, `dimensions`, `user`, batch input) |

#### Legacy Completions
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/completions` | Текстовые комплишены (поддерживается стриминг) |

#### Moderations
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/moderations` | Контент-модерация (batch input, 10 категорий) |

#### Images (DALL-E)
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/images/generations` | Генерация изображений |
| `POST` | `/v1/images/edits` | Редактирование изображений (multipart) |
| `POST` | `/v1/images/variations` | Создание вариаций (multipart) |

#### Audio (Whisper/TTS)
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/audio/transcriptions` | Транскрипция аудио (multipart) |
| `POST` | `/v1/audio/translations` | Перевод аудио на английский (multipart) |
| `POST` | `/v1/audio/speech` | Text-to-speech (возвращает аудио binary) |

#### Batches
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/batches` | Создание батча |
| `GET` | `/v1/batches` | Список батчей (пагинация) |
| `GET` | `/v1/batches/{id}` | Получение батча |
| `POST` | `/v1/batches/{id}/cancel` | Отмена батча |

#### Assistants (20+ эндпоинтов)
| Метод | Путь | Описание |
|-------|------|----------|
| `POST/GET` | `/v1/assistants` | Создание/список ассистентов |
| `GET/POST/DELETE` | `/v1/assistants/{id}` | Получение/обновление/удаление ассистента |
| `POST/GET/DELETE` | `/v1/threads` | Создание/список/удаление тредов |
| `GET/POST` | `/v1/threads/{id}` | Получение/обновление треда |
| `POST/GET` | `/v1/threads/{thread_id}/messages` | Создание/список сообщений |
| `GET/POST` | `/v1/threads/{thread_id}/messages/{id}` | Получение/обновление сообщения |
| `POST/GET` | `/v1/threads/{thread_id}/runs` | Создание/список запусков |
| `GET/POST` | `/v1/threads/{thread_id}/runs/{id}` | Получение/обновление/отмена запуска |
| `POST` | `/v1/threads/{thread_id}/runs/{id}/submit_tool_outputs` | Отправка tool outputs |
| `GET` | `/v1/threads/{thread_id}/runs/{id}/steps` | Список шагов запуска |

**Функции:** Tools (code_interpreter, file_search, functions), file_ids, temperature, top_p, truncation_strategy, tool_choice, response_format, фоновая обработка запусков

#### Fine-tuning
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/fine_tuning/jobs` | Создание fine-tuning job |
| `GET` | `/v1/fine_tuning/jobs` | Список jobs |
| `GET/POST` | `/v1/fine_tuning/jobs/{id}` | Получение/отмена job |
| `GET` | `/v1/fine_tuning/jobs/{id}/events` | Список событий job |
| `GET` | `/v1/fine_tuning/jobs/{id}/checkpoints` | Список чекпойнтов |

#### Files
| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/files` | Загрузка файла (multipart, purposes: fine-tune, assistants, batch, и др.) |
| `GET` | `/v1/files` | Список файлов (пагинация, фильтр по purpose) |
| `GET` | `/v1/files/{id}` | Информация о файле |
| `DELETE` | `/v1/files/{id}` | Удаление файла |
| `GET` | `/v1/files/{id}/content` | Скачивание содержимого файла |

#### Admin/Organization
| Метод | Путь | Описание |
|-------|------|----------|
| `POST/GET/DELETE` | `/v1/api_keys` | Управление API ключами |
| `POST/GET/POST/POST` | `/v1/projects` | Создание/список/обновление/архивирование проектов |
| `GET` | `/v1/users` | Список пользователей |
| `POST/GET/DELETE` | `/v1/invites` | Управление инвайтами |
| `GET` | `/v1/audit_logs` | Список audit логов |
| `GET` | `/v1/organization` | Информация об организации |

#### Административные эндпоинты (Внутренние)
| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/admin/providers` | Список всех провайдеров со статусом |
| `POST/PUT/DELETE` | `/admin/providers` | CRUD провайдеров |
| `POST` | `/admin/providers/{id}/refresh-models` | Запуск обновления моделей |
| `POST` | `/admin/refresh-all-models` | Обновление моделей для всех |
| `GET` | `/admin/stats` | Статистика маршрутизации |
| `GET` | `/admin/health` | Health check |

## 🎭 Виртуальные модели

Виртуальный провайдер `VanPatten` поддерживает три режима маршрутизации:

| Model ID | Режим | Описание |
|----------|-------|----------|
| `vanpatten/auto-free` | `auto-free` | Маршрутизирует **только к бесплатным** моделям среди всех провайдеров |
| `vanpatten/auto` | `auto` | Маршрутизирует ко **всем** моделям (бесплатные + платные) среди всех провайдеров |
| `vanpatten/{model-name}` | `specific` | Маршрутизирует к **конкретной модели** с фейловером на других провайдерах с той же моделью |

### Примеры использования
```bash
# Auto-free: только бесплатные модели
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto-free", "messages": [{"role": "user", "content": "Привет"}]}'

# Auto: все модели (бесплатные + платные)
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto", "messages": [{"role": "user", "content": "Привет"}]}'

# Конкретная модель с фейловером
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/google/gemini-2.0-flash-exp:free", "messages": [{"role": "user", "content": "Привет"}]}'
```

## 🔢 Система приоритетов провайдеров

Провайдеры сортируются по `Priority` (выше = пробуется первым):
```
VanPatten (virtual)          → 200
KiloCode - primary           → 110
KiloCode - secondary         → 108
KiloCode - tertiary          → 106
KiloCode - quaternary        → 104
KiloCode - quinary           → 102
OpenAI                       → 90   (неактивен по умолчанию)
Anthropic                    → 80   (неактивен по умолчанию)
Local Ollama                 → 50   (неактивен по умолчанию)
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
Роутер отслеживает использование по каждому провайдеру и пропускает rate-limited провайдеры при маршрутизации.

## 🔄 Обновление моделей
Модели автоматически обнаруживаются с эндпоинтов `/models` провайдеров каждые `ModelRefreshIntervalMinutes` (по умолчанию: 10).
Ручной запуск: `POST /admin/refresh-all-models`

## 🧪 Тестирование
```bash
# Запуск всех тестов
cd src
dotnet test

# Запуск с покрытием
dotnet test --collect:"XPlat Code Coverage"

# Стресс-тест стриминга
cd tests/VanPattenAIRouter.Tests/LLM-result
python3 test_stream_stability.py
```

## 📁 Структура проекта
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
| Host Path | Container Path | Назначение |
|-----------|----------------|------------|
| `./data` | `/app/data` | Persistence SQLite базы данных |
| `./src/appsettings.json` | `/app/appsettings.json` | Конфиг (read-only, hot reload) |

## 🔧 Переменные окружения
| Переменная | Описание | По умолчанию |
|------------|-----------|--------------|
| `ASPNETCORE_ENVIRONMENT` | Имя окружения | `Development` |
| `ASPNETCORE_URLS` | URL для прослушивания | `http://+:8080` |
| `OPENAI_API_KEY` | OpenAI API ключ (для полных функций) | — |

## 🛠️ Устранение неполадок

### Модели не появляются
- Проверьте `IsActive: true` в конфиге провайдера
- Проверьте валидность API ключа
- Запустите ручное обновление: `POST /admin/refresh-all-models`
- Проверьте логи: `docker-compose logs -f`

### Запросы падают с 500
- Проверьте rate limits провайдеров в `/admin/stats`
- Убедитесь, что `BaseUrl` провайдера доступен из контейнера
- Проверьте логи контейнера для деталей ошибок от upstream

### Проблемы с базой данных
- Установите `"RecreateOnStartup": true` в `appsettings.json`, рестарт, затем верните `false`
- Или удалите `data/vanpatten-airrouter.db` и рестарт

### Конфиг не перезагружается
- Убедитесь, что `appsettings.json` смонтирован как volume (read-only OK)
- Файл должен изменяться на хосте (не в контейнере) для срабатывания reload

## 🔒 Соображения безопасности
- **API ключи в конфиге** — Храните безопасно; используйте Docker secrets / Azure Key Vault в продакшене
- **Нет аутентификации** — У роутера нет auth; ставьте за reverse proxy с auth в продакшене
- **Rate limits** — Настройте лимиты на провайдера чтобы избежать неожиданных расходов
- **Сеть** — Запускайте в изолированной сети; не expon'ьте напрямую в интернет

## 🚀 Продакшн развёртывание
1. **Reverse Proxy** — nginx/Traefik для TLS termination и auth
2. **Secrets** — Вынесите API ключи в Docker secrets / Azure Key Vault / AWS Secrets Manager
3. **Мониторинг** — Health checks (`/admin/health`), логирование, метрики
4. **Масштабирование** — Одного инстанса достаточно; SQLite не поддерживает concurrent writers
5. **Бэкапы** — Регулярно бэкапьте `data/vanpatten-airrouter.db`

## 📄 Лицензия
MIT License — см. файл LICENSE для деталей.

## 🤝 Вклад в проект
1. Форкните репозиторий
2. Создайте feature branch
3. Внесите изменения с тестами
4. Отправьте PR

## 📞 Поддержка
По вопросам и запросам фичей — открывайте GitHub issue.