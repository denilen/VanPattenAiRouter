# VanPatten AI Router

**Русский** | [English](README.md)

Лёгкий API-шлюз, совместимый с OpenAI, для маршрутизации запросов (аля openrouter.ai) к множественным LLM-провайдерам с интеллектуальным фейловерейтом, ограничением частоты запросов и поддержкой виртуальных моделей.

## Возможности

- **OpenAI-совместимый API** — Прямая замена OpenAI API (`/v1/chat/completions`, `/v1/models`, `/v1/embeddings`)
- **Поддержка множественных провайдеров** — Маршрутизация к KiloCode, OpenAI, Anthropic, Ollama и кастомным OpenAI-совместимым эндпоинтам
- **Виртуальные модели** — `vanpatten/auto-free` автоматически маршрутизирует к бесплатным моделям между провайдерами; `vanpatten/auto` маршрутизирует ко всем моделям
- **Интеллектуальный фейловер** — Автоматические повторы с экспоненциальным бэкоффом между провайдерами на основе приоритета
- **Ограничение частоты запросов (Rate Limiting)** — Лимиты на провайдера (запросов в день/час, токенов в день/час) с автоматическим принудительным переключением
- **Приоритет провайдеров** — Настраиваемая система приоритетов для порядка выбора провайдеров
- **Обновление моделей** — Автоматическое фоновое обнаружение моделей с эндпоинтов `/models` провайдеров
- **Admin API** — REST-эндпоинты для управления провайдерами, просмотра статистики и запуска обновления моделей
- **SQLite Persistence** — Встроенная база данных для состояния провайдеров, rate limits и каталога моделей
- **Hot Config Reload** — Изменения в `appsettings.json` подхватываются автоматически без перезапуска

## Архитектура

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Client App    │────▶│  VanPatten AI    │────▶│  KiloCode       │
│  (OpenAI SDK)   │     │  Router          │     │  Provider 1     │
└─────────────────┘     │                  │     │  (Priority 110) │
                        │  - Routing       │     └─────────────────┘
                        │  - Failover      │     ┌─────────────────┐
                        │  - Rate Limit    │────▶│  KiloCode       │
                        │  - Virtual Models│     │  Provider 2     │
                        │  - Model Cache   │     │  (Priority 108) │
                        └──────────────────┘     └─────────────────┘
                                  |                    ...
                        ┌──────────────────┐     ┌──────────────────┐
                        │  SQLite DB       │     │  OpenAI /        │
                        │  - Providers     │     │  Anthropic /     │
                        │  - Models        │     │  Ollama          │
                        │  - Rate Limits   │     │  (Lower Priority │
                        └──────────────────┘     └──────────────────┘
```

## Быстрый старт

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
           "RateLimit": "RequestsPerDay": 0,
                        "TokensPerDay": 0,
                        "RequestsPerHour": 200,
                        "TokensPerHour": 500000
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

## Конфигурация

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
|-----|----------|
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
- `RecreateOnStartup` — установите `true` для однократного пересоздания БД при следующем рестарте (полезно после изменений конфигурации), затем верните `false`

## API Эндпоинты

### OpenAI-совместимые эндпоинты

| Метод | Путь | Описание |
|-------|------|----------|
| `POST` | `/v1/chat/completions` | Чат-комплишены (поддерживается стриминг) |
| `GET` | `/v1/models` | Список всех доступных моделей |
| `POST` | `/v1/embeddings` | Создание эмбеддингов |

### Административные эндпоинты

| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/admin/providers` | Список всех провайдеров со статусом |
| `GET` | `/admin/providers/{id}` | Детали провайдера |
| `POST` | `/admin/providers` | Создание нового провайдера |
| `PUT` | `/admin/providers/{id}` | Обновление провайдера |
| `DELETE` | `/admin/providers/{id}` | Удаление провайдера |
| `POST` | `/admin/providers/{id}/refresh-models` | Запуск обновления моделей для провайдера |
| `POST` | `/admin/refresh-all-models` | Обновление моделей для всех провайдеров |
| `GET` | `/admin/stats` | Статистика маршрутизации |
| `GET` | `/admin/health` | Health check |

### Виртуальные модели

Виртуальный провайдер `VanPatten` поддерживает три режима маршрутизации:

| Model ID | Режим | Описание |
|----------|-------|----------|
| `vanpatten/auto-free` | `auto-free` | Маршрутизирует **только к бесплатным** моделям среди всех провайдеров |
| `vanpatten/auto` | `auto` | Маршрутизирует ко **всем** моделям (бесплатные + платные) среди всех провайдеров |
| `vanpatten/{model-name}` | `specific` | Маршрутизирует к **конкретной модели** с фейловером на других провайдерах с той же моделью |

#### Подробности режимов

**1. `auto-free` (`vanpatten/auto-free`)**
- Учитывает только модели, помеченные как бесплатные
- Пробует провайдеры в порядке приоритета
- При rate limit или ошибке переходит к следующему провайдеру

**2. `auto` (`vanpatten/auto`)**
- Учитывает все модели (бесплатные и платные)
- Пробует провайдеры в порядке приоритета
- При rate limit или ошибке переходит к следующему провайдеру

**3. Конкретная модель (например, `vanpatten/google/gemini-2.0-flash-exp:free`)**
- Пользователь указывает точную модель: `vanpatten/{model-id}`
- Роутер находит всех провайдеров, у которых есть эта **точная** модель (должна быть бесплатной и включённой)
- Пробует провайдеры в порядке приоритета
- Если провайдер попадает в rate limit или падает — **автоматически фейловирит на следующего провайдера с той же моделью**
- Откат на другие модели происходит только если ни у одного провайдера нет запрашиваемой модели

#### Как работает виртуальная маршрутизация

При использовании любой виртуальной модели роутер:
1. Определяет виртуального провайдера и режим из имени модели
2. Находит кандидатов моделей в зависимости от режима:
   - `auto-free`: Все бесплатные модели от активных провайдеров
   - `auto`: Все модели от активных провайдеров
   - `specific`: Только запрошенная модель от провайдеров, у которых она есть
4. Группирует по провайдерам, сортирует по приоритету провайдера (выше = первым)
5. Перебирает провайдеры в порядке приоритета, пробуя модели до успеха
6. Автоматически пропускает rate-limited провайдеров
7. Возвращает первый успешный ответ с указанием фактического провайдера/модели в ответе

### Примеры использования

```bash
# Auto-free: только бесплатные модели
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto-free", "messages": [{"role": "user", "content": "Привет"}]}'

# Auto: все модели (бесплатные + платные)
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/auto", "messages": [{"role": "user", "content": "Привет"}]}'

# Конкретная модель с фейловером: пробует эту модель на каждом провайдере
curl -X POST http://localhost:10080/v1/chat/completions \
  -d '{"model": "vanpatten/google/gemini-2.0-flash-exp:free", "messages": [{"role": "user", "content": "Привет"}]}'
```

## Система приоритетов провайдеров

Провайдеры сортируются по `Priority` (выше = пробуется первым). Текущие значения по умолчанию:

```
VanPatten (virtual)          → 200  (маршрутизирует к реальным провайдерам)
KiloCode - primary           → 110
KiloCode - secondary         → 108
KiloCode - tertiary          → 106
KiloCode - quaternary        → 104
KiloCode - quinary           → 102
OpenAI                       → 90   (неактивен по умолчанию)
Anthropic                    → 80   (неактивен по умолчанию)
Local Ollama                 → 50   (неактивен по умолчанию)
```

Все аккаунты KiloCode имеют более высокий приоритет, чем платные провайдеры. Настройте под свои предпочтения.

## Ограничение частоты (Rate Limiting)

У каждого провайдера могут быть лимиты:

```json
"RateLimit": {
  "MaxRequestsPerDay": 200,
  "MaxTokensPerHour": 500000
}
```

Роутер отслеживает использование по каждому провайдеру и пропускает rate-limited провайдеры при маршрутизации.

## Обновление моделей

Модели автоматически обнаруживаются с эндпоинтов `/models` провайдеров каждые `ModelRefreshIntervalMinutes` (по умолчанию: 10). Можно также запустить вручную:

```bash
curl -X POST http://localhost:10080/admin/refresh-all-models
```

Классификация бесплатных/платных для моделей KiloCode использует список `FreeModels.KiloCode` в конфиге. Для остальных провайдеров используются эвристические паттерны (`:free`, `-free`, `/free`, `free-`).

## Тестирование

```bash
# Запуск всех тестов
cd src
dotnet test

# Запуск с покрытием
dotnet test --collect:"XPlat Code Coverage"
```

## Структура проекта

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
│   ├── Services/
│   │   ├── RoutingService.cs   # Core routing & failover logic
│   │   ├── ProviderService.cs  # Provider CRUD & queries
│   │   ├── ModelRefreshService.cs # Background model discovery
│   │   └── RetryPolicyService.cs  # Retry with exponential backoff
│   ├── Data/
│   │   ├── AiRouterDbContext.cs     # EF Core SQLite context
│   │   └── SeedData.cs         # Initial provider/model seeding
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

| Host Path | Container Path | Назначение |
|-----------|----------------|------------|
| `./data` | `/app/data` | Persistence SQLite базы данных |
| `./src/appsettings.json` | `/app/appsettings.json` | Конфиг (read-only, hot reload) |

## Переменные окружения

| Переменная | Описание | По умолчанию |
|------------|----------|--------------|
| `ASPNETCORE_ENVIRONMENT` | Имя окружения | `Development` |
| `ASPNETCORE_URLS` | URL для прослушивания | `http://+:8080` |

## Устранение неполадок

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
- Убедитесь, что `appsettings.json` смонтирован как volume (read-only нормально)
- Файл должен изменяться на хосте (не в контейнере) для срабатывания reload

## Соображения безопасности

- **API ключи в конфиге** — Храните `appsettings.json` безопасно; рассмотрите Docker secrets или Azure Key Vault в продакшене
- **Нет аутентификации** — У самого роутера нет auth; ставьте за reverse proxy с auth в продакшене
- **Rate limits** — Настройте лимиты на провайдера чтобы избежать неожиданных расходов
- **Сеть** — Запускайте в изолированной сети; не expon'ьте напрямую в интернет

## Продакшн развёртывание

1. **Reverse Proxy** — Используйте nginx/Traefik для TLS termination и auth
2. **Secrets** — Вынесите API ключи в Docker secrets / Azure Key Vault / AWS Secrets Manager
3. **Мониторинг** — Добавьте health checks (`/admin/health`), логирование, метрики
4. **Масштабирование** — Одного инстанса достаточно для большинства случаев; SQLite не поддерживает concurrent writers
5. **Бэкапы** — Регулярно бэкапьте `data/vanpatten-airrouter.db`

## Лицензия

MIT License — см. файл LICENSE для деталей.

## Вклад в проект

1. Форкните репозиторий
2. Создайте feature branch
3. Внесите изменения с тестами
4. Отправьте PR

## Поддержка

По вопросам и запросам фичей — открывайте GitHub issue.