# AGENTS.md — Finance & Food Tracker Mini App

Корневой гид для AI-агентов и разработчиков. Читать в первую очередь перед любой задачей.

## Что это за проект

Telegram Mini App для **личного учёта финансов и питания**:
- ручной ввод доходов/расходов и категорий;
- загрузка фото чеков → AI-разбор на позиции и категории;
- загрузка фото блюд → оценка калорий и БЖУ (диапазоном);
- отчёты: расходы по категориям/месяцам/магазинам, дневник питания.

Backend + БД + reverse-proxy разворачиваются через **Docker Compose** на своём VPS. Модель данных с самого начала учитывает `UserId` (мультипользовательский задел).

Источник требований — `TZ_finance_food_tracker_miniapp.md`. При конфликте ТЗ и этих доков **приоритет у ТЗ**, а расхождение нужно зафиксировать в `docs/DECISIONS.md`.

## Карта документации

| Файл | Что внутри |
|---|---|
| `TZ_finance_food_tracker_miniapp.md` | Исходное техническое задание (источник истины) |
| `docs/PLAN.md` | Поэтапный план MVP → расширение, чек-листы задач |
| `docs/STACK.md` | Технологический стек, версии, обоснования |
| `docs/ARCHITECTURE.md` | Архитектура, модель данных, API, AI-пайплайн, инфра |
| `docs/DECISIONS.md` | Журнал архитектурных решений (ADR) и открытые вопросы |
| `docs/SERVER.md` | Боевой сервер: SSH-доступ, деплой, архитектура размещения, TLS |
| `AGENTS.md` | Этот файл: правила работы, команды, конвенции |

## План репозитория (целевой)

```text
.
├── AGENTS.md                 # этот файл
├── TZ_finance_food_tracker_miniapp.md
├── docs/                     # документация и планы
├── docker-compose.yml        # оркестрация сервисов
├── .env.example              # шаблон секретов (без реальных значений!)
├── Caddyfile                 # reverse-proxy + TLS + статика
├── proxy/Dockerfile          # node build web -> caddy:alpine (отдаёт /srv)
├── deploy/                   # server-override compose для боевого VPS
├── api/                      # ASP.NET Core backend
│   ├── FinanceFoodTracker.sln
│   ├── Dockerfile            # multi-stage: sdk -> aspnet:8-alpine
│   ├── src/{Api,Application,Domain,Infrastructure}
│   └── tests/FinanceFoodTracker.Tests
└── web/                      # React Mini App (Vite + TS)
```

## Стек (кратко)

- **Frontend:** React + `@telegram-apps/sdk`, тема через `themeParams`.
- **Backend:** ASP.NET Core (.NET 8/9), minimal API или контроллеры.
- **БД:** PostgreSQL 16 + EF Core.
- **AI:** OpenCode Go API (vision) через абстракции `IReceiptAnalyzer` / `IFoodImageAnalyzer`.
- **Инфра:** Docker Compose, alpine-образы, Caddy/Nginx, TLS.
- Полные детали — в `docs/STACK.md`.

## Боевой сервер

Приложение задеплоено на VPS; на том же сервере живёт чужой сайт `pinsk-elektrik.by`, поэтому там используется **системный nginx**, а Caddy отключён. Полная информация — `docs/SERVER.md`.

```powershell
ssh -i "$env:USERPROFILE\.ssh\fft_deploy" root@45.128.205.200
# репозиторий на сервере: /home/FoodTrack/FoodAndFinanceTrack
```

Приватный ключ в репозиторий не кладём; секреты — только в `.env` на сервере.

## Команды

### Backend (`api/`)
```bash
dotnet restore FinanceFoodTracker.sln
dotnet build   FinanceFoodTracker.sln
dotnet test
dotnet run --project src/Api
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
dotnet ef database update      --project src/Infrastructure --startup-project src/Api
```

### Frontend (`web/`)
```bash
npm install
npm run dev
npm run build
npm run lint
```

### Docker / инфра (корень)
```bash
docker compose up -d --build
docker compose logs -f api
docker compose down
```

### Проверки перед завершением задачи
1. `dotnet build` (backend) и `npm run build` + `npm run lint` (frontend) — без ошибок.
2. `dotnet test` / фронтовые тесты — зелёные.
3. Обновить статусы в `docs/PLAN.md`.
4. Архитектурные изменения зафиксировать в `docs/DECISIONS.md`.

## Обязательные правила (не нарушать)

1. **`initData` проверяется только на сервере** через HMAC с бот-токеном на каждом запросе. Клиенту не доверять (см. ТЗ §9).
2. **Деньги — только `numeric(12,2)`**, никогда `float`/`double` (ТЗ §4.2).
3. **AI вызывается только через абстракции** `IReceiptAnalyzer` / `IFoodImageAnalyzer`. Прямые HTTP-вызовы провайдера из контроллеров запрещены (ТЗ §7).
4. **Ответы AI — строго structured JSON**, сохранять сырой ответ в `ai_raw_response` (jsonb).
5. **Секреты — только через env / `.env`**, никогда в репозитории. В репо — лишь `.env.example`.
6. **Docker-образы — alpine** там, где возможно; multi-stage сборка .NET; заданы `mem_limit`/`cpus` и лимиты логов `max-size`/`max-file`.
7. **Оригиналы изображений хранятся всегда** (для повторного анализа).
8. **Приложение работает без AI**: ручной ввод и отчёты не должны зависеть от доступности провайдера.

## Конвенции разработки

- Архитектура backend: слоистая (Api → Application → Domain/Infrastructure). Бизнес-логика не в контроллерах.
- API — под префиксом `/api`, DTO отделены от EF-сущностей.
- Комментарии в коде — только по необходимости; код самодокументируемый.
- Названия веток: `feature/<этап>-<кратко>`, напр. `feature/stage3-receipt-ai`.
- Каждое изменение, затрагивающее схему БД, — через EF-миграцию (не править БД руками).
- Асинхронные операции (анализ) возвращают статус и результат отдельно (polling).

## Рабочий процесс агента

1. Прочитать `TZ` (по необходимости) + `docs/PLAN.md` → определить текущий этап.
2. Сверить действие с обязательными правилами выше.
3. Реализовать минимально достаточное изменение, покрыть тестами.
4. Выполнить проверки из раздела «Команды».
5. Отметить прогресс в `docs/PLAN.md`; новые решения — в `docs/DECISIONS.md`.
6. **Не коммитить без явного запроса пользователя.**
