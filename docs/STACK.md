# Технологический стек

Описание стека и обоснование выбора (по ТЗ §2, §8). Версии фиксировать при скаффолдинге.

## Сводная таблица

| Слой | Технология | Версия (план) | Комментарий |
|---|---|---|---|
| Frontend | React + Vite + TypeScript | React 18+, Vite 5+ | Легкий SPA Mini App |
| Telegram SDK | `@telegram-apps/sdk` (или `telegram-web-app.js`) | актуальная | `initData`, `themeParams` |
| Backend | ASP.NET Core | .NET 8 (LTS) | Minimal API / контроллеры |
| ORM | Entity Framework Core + Npgsql | EF Core 8 | Миграции, слоистая архитектура |
| БД | PostgreSQL | 16-alpine | Реляционные данные |
| Кэш/очередь | Redis (опционально) | 7-alpine | **Не подключать в v1**, только при необходимости очереди |
| Хранилище файлов | Volume `uploads` → MinIO при росте | — | На старте volume, позже S3 |
| AI | OpenCode Go API (vision) + OCR | внешний HTTPS | Через абстракции |
| Reverse-proxy | Caddy | caddy:alpine | TLS + отдача статики, простой конфиг |
| Оркестрация | Docker Compose | — | Единый `docker-compose.yml` |
| Контейнеры | alpine-образы | — | Меньше вес/поверхность атаки |

## Компоненты и обоснование

### Frontend
- **React + TypeScript** — типизация DTO/состояния, экосистема.
- **Vite** — быстрый дев-сервер и маленький бандл.
- **`@telegram-apps/sdk`** — доступ к `initData`, `themeParams`, нативным функциям Telegram.
- Сборка — статика, раздаётся reverse-proxy.

### Backend
- **ASP.NET Core (.NET 8 LTS)** — соответствие стеку команды, высокая производительность на малом VPS.
- Слои: `Api` → `Application` → `Domain` / `Infrastructure`.
- **Minimal API** или контроллеры — на выбор; единый стиль на весь проект.
- **EF Core + Npgsql** — миграции как единственный способ менять схему.
- Асинхронный анализ — фоновая обработка (hosted service/канал), результат через polling.

### База данных
- **PostgreSQL 16-alpine**.
- Деньги — `numeric(12,2)`.
- `jsonb` для `ai_raw_response` (отладка промптов без повторных запросов).
- Тюнинг под малый сервер: `shared_buffers`, `max_connections` (не дефолт).

### AI-слой
- Абстракции **`IReceiptAnalyzer`** и **`IFoodImageAnalyzer`** — провайдер заменяем без правки бизнес-логики.
- **OpenCode Go API** (vision) + OCR-слой.
- Ответ — strict JSON (structured output).
- Валидация на backend: сумма позиций ≈ итог, обязательные поля, порог `confidence`.
- Питание — всегда диапазон калорий/БЖУ с пометкой «оценка».

### Инфраструктура и ресурсы
- **Docker Compose**, одна внутренняя сеть `internal`, наружу — только proxy (80/443).
- **Caddy** для TLS/роутинга (`/api`, статика).
- Лимиты в compose:
  - `api`: `mem_limit: 512m`, `cpus: 1.0`
  - `db`: `mem_limit: 512m`, `cpus: 1.0`
  - `proxy`: `mem_limit: 128m`, `cpus: 0.5`
  - суммарно ~1.1 ГБ RAM.
- Логи: `x-logging` (json-file, `max-size: 10m`, `max-file: 3`, `compress: true`) → ≤ ~30 МБ на сервис, ≤ ~90 МБ суммарно; те же лимиты глобально в `/etc/docker/daemon.json`.
- **Multi-stage Dockerfile**: сборка в `sdk`, рантайм `aspnet:8-alpine` без SDK.
- Цель по весу: образы без Postgres ≤ ~300–400 МБ.

## Переменные окружения (`.env`)

| Переменная | Назначение |
|---|---|
| `BOT_TOKEN` | Telegram bot token (валидация `initData`) |
| `OPENCODE_GO_KEY` | Ключ OpenCode Go API |
| `DB_PASSWORD` | Пароль PostgreSQL |
| `ConnectionStrings__Default` | Строка подключения EF Core |
| `AI_CONFIDENCE_THRESHOLD` | Порог уверенности → `needs_review` |
| `UPLOAD_MAX_SIZE_MB` | Лимит загрузки (по умолчанию 8) |

> Реальные значения — только в `.env` (не в репозитории). В репо — `.env.example`.

## Нефункциональные ориентиры

- CRUD API ≤ 300 мс на типовом VPS.
- Анализ чека/блюда — асинхронно, UI сразу получает `pending`.
- Работоспособность без AI (ручной ввод/отчёты).
- HTTPS обязателен (Telegram WebView).
