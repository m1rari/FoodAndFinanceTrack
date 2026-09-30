# APP_CONTEXT.md

> Контекст для продуктового и технического анализа Telegram Mini App «Финансы и питание».
> **Дата анализа:** 2026-09-30 · **Ветка:** `master` · **Commit:** `47aef5b`
> **Что выполнено:** анализ по коду и конфигам (без изменений кода). Сборки/линт проходили ранее в этом репозитории (`dotnet build`, `npm run build`, `npm run lint`), тестов 70 (`[Fact]`) — зелёные; приложение развёрнуто на VPS и проверялось HTTP smoke-тестами. Визуальное качество UI не оценивалось (скриншоты не анализировались).
> Корневого `README.md` нет; `web/README.md` — стандартный шаблон Vite.

## 1. Краткое описание

- **Что это:** Telegram Mini App (TWA) для личного учёта **финансов** (доходы/расходы, чеки, банковские выписки) и **питания** (фото блюд, калории/БЖУ, дневник).
- **Задачу решает:** ручной ввод операций + автоматический разбор фото чеков, PDF-выписок (Беларусбанк/Приорбанк) и фото блюд с помощью внешнего AI (OpenCode Go), отчёты по категориям/магазинам, задел под мультипользовательность.
- **Пользователи:** в коде данных о реальной аудитории нет. Модель `User` мультипользовательская, есть шаринг блюд между пользователями и Telegram-бот — предполагается личное использование (один-несколько пользователей). Это **предположение**, не подтверждённый факт.
- **Стадия:** функционально развитый MVP/ранний production. Реализованы этапы 1–5 из `docs/PLAN.md` (каркас, чеки, AI-разбор чеков, питание), UX-редизайн, бот, матчинг, шаринг. Не реализованы: автокатегоризация (этап 4), расширенные отчёты (этап 6), эксплуатационные задачи (этап 7). Статусы — в `docs/PLAN.md`.

## 2. Реализованные возможности

- **Авторизация через Telegram `initData`** — реализована. `api/src/Infrastructure/Security/TelegramInitDataValidator.cs`, `api/src/Api/Middleware/TelegramInitDataMiddleware.cs`, `api/src/Api/Controllers/AuthController.cs` (`POST /api/auth/telegram`).
- **Ручные операции** (доход/расход, категория, комментарий, дата) — реализовано. `api/src/Application/Transactions/TransactionService.cs`, `web/src/screens/TransactionFormScreen.tsx`.
- **Категории** (системные, сидируются) — реализовано. `api/src/Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`, `CategoryService.cs`.
- **Отчёт-свод по категориям за период** — реализовано, переводы исключаются. `api/src/Application/Reports/ReportService.cs`, `web/src/screens/ReportScreen.tsx`. Топ магазинов — частично (вычисляется на клиенте).
- **Загрузка фото чека + AI-разбор** — реализовано. `ReceiptService.cs`, `OpenCodeGoReceiptAnalyzer.cs`, `ReceiptProcessor.cs`, фоновая очередь `ReceiptProcessingWorker.cs`.
- **Проведение покупки в операции / привязка к существующей** — реализовано. `POST /api/receipts/{id}/confirm`, `.../link/{transactionId}`, `.../matches`.
- **Матчинг ручной операции с чеком** (сумма ±0.01, дата ±1 день) — реализовано. `ReceiptService.GetMatchesAsync`.
- **Импорт банковских выписок (PDF)** — реализовано: Беларусбанк и Приорбанк. `IPdfTextExtractor`/`PdfPigTextExtractor.cs`, `IStatementAnalyzer`/`OpenCodeGoStatementAnalyzer.cs` (чанкинг), `StatementProcessor.cs`, `StatementService.cs`, `web/src/screens/StatementReviewScreen.tsx`.
- **Переводы/снятие наличных не влияют на доходы/расходы** — реализовано через флаг `Transaction.IsTransfer` (`ReportService`, дневные итоги на клиенте).
- **Питание: фото/описание блюда → ккал и БЖУ диапазоном** — реализовано. `OpenCodeGoFoodImageAnalyzer.cs`, `FoodLogProcessor.cs`, `web/src/screens/FoodScreen.tsx`, `FoodDetailScreen.tsx`.
- **Разбиение приёма пищи на компоненты** (борщ + пюре → отдельные записи, общий `MealGroupId`) — реализовано. `FoodLogProcessor.ApplyResult`, `FoodLog.MealGroupId`.
- **Граммовка порции + повторный разбор** — реализовано. `FoodLog.PortionGrams`, `POST /api/food-logs/{id}/reanalyze`.
- **Избранные/недавние блюда** — реализовано. `SavedDish`, `SavedDishService.cs`, `FoodScreen` (поп-ап «+ Блюдо»).
- **Шаринг блюда по ссылке** (`t.me/<bot>?startapp=fd-<token>`) — реализовано. `FoodShare`, `FoodShareService.cs`, `FoodSharesController.cs`, дедуп в `App.tsx`.
- **Telegram-бот: приём чеков/еды и текстовых описаний, режимы «Чек»/«Еда»** — реализовано (голосовые — вежливый отказ). `TelegramController.cs`, `Infrastructure/Telegram/*`.
- **Удаление операций / покупок** — реализовано. `TransactionService.DeleteAsync`, `ReceiptService.DeleteAsync`.
- **Автокатегоризация и обучение (`category_rules`)** — **упомянута в ТЗ/PLAN, реализация не найдена**: сущность `CategoryRule` и таблица есть (задел), но сервиса/контроллера нет. Этап 4 не начат.
- **`IFoodImageAnalyzer` OCR-слой как отдельный компонент** — не найден; распознавание выполняет vision-модель.
- **Финансовые отчёты: тренды по месяцам, «еда vs калории», экспорт** — не найдено (этап 6).

## 3. Пользовательские сценарии

По коду:
1. **Открытие:** пользователь открывает Mini App из бота. `web/src/App.tsx` вызывает `initTelegram()` (`telegram/init.ts`), получает `initData`, отправляет в `POST /api/auth/telegram`; при успехе — рабочее приложение, при ошибке — экран ошибки (`App.tsx`, `.centered.error`).
2. **Первый вход:** upsert пользователя и создание счёта `Основной` (BYN) — `UserService.GetOrCreateAsync`. Показывается вкладка «Операции» (по умолчанию, либо из `?tab=`).
3. **Добавление операции вручную:** «+ Добавить» → `AddSheet` → «Вручную» → форма → сохранение (`TransactionFormScreen`; при доступности — нативная `MainButton`).
4. **Чек:** `AddSheet` → камера/галерея → сжатие на клиенте (`utils/image.ts`) → `POST /api/receipts` → статус `Pending` → фон разбирает → клиент опрашивает `GET /api/receipts/{id}` → экран покупки → при необходимости правки позиций → «Провести покупку» (создаёт операции `source=receipt`) или привязка к найденной ручной операции.
5. **Питание:** вкладка «Питание» → «+ Блюдо» → фото и/или описание → `POST /api/food-logs` или `/api/food-logs/text` → фон → карточка блюда; можно править, менять граммовку и «Распознать заново», делиться, добавлять в избранное.
6. **Выписка:** `AddSheet` → PDF → `POST /api/statements` (сразу `Pending`) → фон разбирает → экран проверки (правки, исключение, пометка «перевод», связка с найденными операциями) → «Провести».
7. **Бот:** режимы «Чек»/«Еда», фото/текст → фон → ответ в чат; результат появляется в приложении.

**Альтернативные состояния (по коду):** загрузка — скелетоны (`Skeleton.tsx`) и текст «…»; ошибки — inline-текст и `ProblemDetails` от API; пустые состояния — блоки `.empty` с CTA; `Pending` — polling; повторный вход — upsert пользователя; невалидная ссылка шаринга — молча игнорируется (`App.tsx`).
**Проверено запуском:** базовые HTTP-сценарии (auth, операции, отчёты, чеки, блюда, выписки, шаринг) на боевом сервере. **Не проверено:** фактический визуальный UX на устройствах, поведение нативной `MainButton`/`BackButton` во всех клиентах.

## 4. Экраны и навигация

Навигация — нижний док из 3 вкладок (`App.tsx`); оверлеи-экраны перекрывают контент и используют нативный `BackButton` (`hooks/useBackButton.ts`). Состояние вкладки и фильтров синхронизируется с query-параметрами (`utils/url.ts`, `writeUrlParams`).

| Экран | Назначение | Основные действия | Файл |
|---|---|---|---|
| Операции (вкладка) | Список по дням, покупки, свод | Фильтры (период/тип/категория), «+ Добавить», открыть покупку/операцию | `web/src/screens/OperationsScreen.tsx` |
| Питание (вкладка) | Дневник блюд по дню, итоги | Переключение дня, «+ Блюдо», избранные/недавние, открыть блюдо | `web/src/screens/FoodScreen.tsx` |
| Отчёты (вкладка) | Свод за период | Пресеты периода, категории, топ магазинов | `web/src/screens/ReportScreen.tsx` |
| Форма операции (оверлей) | Создание/правка операции | Сохранить, удалить | `web/src/screens/TransactionFormScreen.tsx` |
| Покупка (оверлей) | Карточка чека | Правки позиций, проведение/привязка, удаление | `web/src/screens/PurchaseScreen.tsx` |
| Блюдо (оверлей) | Карточка блюда | Правки, граммовка, реанализ, избранное, шаринг, удаление | `web/src/screens/FoodDetailScreen.tsx` |
| Выписка (оверлей) | Проверка разбора PDF | Правки, перевод, исключение, привязка, проведение | `web/src/screens/StatementReviewScreen.tsx` |
| Поп-ап «Добавить» | Хаб добавления | Вручную / чек камерой / из галереи / выписка | `web/src/components/AddSheet.tsx` |

Переиспользуемые компоненты: `BottomSheet.tsx` (`role="dialog"`, Escape), `Skeleton.tsx`. Общие утилиты: `utils/date.ts`, `utils/format.ts`, `utils/image.ts`, `utils/url.ts`.

## 5. Техническая архитектура

- **Стек:** ASP.NET Core **net8.0**; EF Core **8.0.11** + Npgsql; PostgreSQL 16-alpine; `PdfPig` 0.1.9; `Microsoft.Extensions.Http` 8.0.1; Swashbuckle 6.4.0. Frontend: **React 19**, Vite 8, TypeScript ~6.0, `@telegram-apps/sdk` 3.11, линтер `oxlint`.
- **Слои backend:** `Api → Application → Domain / Infrastructure` (см. `docs/ARCHITECTURE.md`, `AGENTS.md`).
- **Модули:** контроллеры в `api/src/Api/Controllers`; use-cases в `Application/*`; сущности в `Domain/Entities`; EF/хранилище/AI/Telegram в `Infrastructure/*`.
- **Внешние сервисы:** OpenCode Go API (OpenAI-совместимый, `https://opencode.ai/zen/go/v1`) — vision и текст; Telegram Bot API.
- **Управление состоянием frontend:** локальное состояние React (`useState`/`useEffect`), без Redux/кэша; `initData` в модуле `api/client.ts`; URL — для вкладки и фильтров.
- **Очереди/фон:** `Channel<Guid>` + `BackgroundService` для чеков, блюд и выписок; восстановление `Pending` при старте (`ReceiptProcessingWorker`, `FoodLogProcessingWorker`, `StatementProcessingWorker`).
- **AI-абстракции:** `IReceiptAnalyzer`, `IFoodImageAnalyzer`, `IStatementAnalyzer`; общий клиент `OpenCodeGoVisionClient`; при пустом ключе — заглушки (`Stub*`).
- **Схема данных (текст):**
  `Telegram initData → middleware (HMAC) → ICurrentUser → контроллер → сервис (Application) → EF (Infrastructure) → PostgreSQL`.
  Фото/PDF: `multipart → IFileStorage → volume uploads → запись в очередь → worker → AI → БД → клиент (polling)`.
  Бот: `Telegram webhook → TelegramController → GetOrCreateAsync → сервис → фон → sendMessage`.

**Вне репозитория:** боевой `.env`, системный nginx-конфиг и TLS, DNS-зона, актуальные ключи/токены, менеджер процессов соседнего сайта. Подробности — `docs/SERVER.md`.

## 6. Данные и API

- **Сущности (`Domain/Entities`):** `User`, `Account`, `Category`, `CategoryRule` (задел), `Transaction`, `Receipt`, `ReceiptItem`, `FoodLog`, `SavedDish`, `FoodShare`, `Statement`. Базовый `Entity` (Guid `Id`).
- **Хранение:** PostgreSQL; деньги — `numeric(12,2)`; `ai_raw_response`/`parsed_operations` — `jsonb`; изображения/PDF — файлы в volume `uploads` (`LocalFileStorage`, `StorageOptions`).
- **Ключевые API (маршруты найдены в контроллерах):**
  - `POST /api/auth/telegram`;
  - `/api/transactions` (GET/POST/PATCH/DELETE);
  - `/api/categories`; `/api/reports/summary`;
  - `/api/receipts` (POST, GET list, GET `{id}`, `/image`, `/items` POST/PATCH, `/confirm`, `/matches`, `/link/{transactionId}`, DELETE);
  - `/api/food-logs` (POST, `/text`, GET list/`{id}`, `/image`, PATCH, DELETE, `/favorite`, `/share`, `/reanalyze`);
  - `/api/saved-dishes` (GET, `/{id}/diary`, PATCH, DELETE);
  - `/api/food-shares/{token}` (GET, POST `/claim`);
  - `/api/statements` (POST, GET `{id}`, `/matches`, `/confirm`);
  - `POST /api/telegram/webhook`.
- **Валидация/правила:** сумма > 0; тип категории соответствует типу операции; лимит загрузки 8 МБ (фото) / 12 МБ (PDF); определение типа изображения по magic bytes (`ImageContent`); допуск суммы чека (`ReceiptProcessor`), порог `confidence` (`AiOptions.ConfidenceThreshold`); переводы исключаются из отчётов; защита от повторного `confirm`.
- **Доступ:** данные разделены по `UserId`; `initData` проверяется на каждом защищённом запросе (`TelegramInitDataMiddleware`), кроме `/api/auth/*` и `/api/telegram`.
- **Внешние интеграции:** OpenCode Go API; Telegram Bot API; `setWebhook` при старте (`TelegramWebhookSetup`).

## 7. Интеграция с Telegram

- **Запуск:** `web/index.html` подключает `telegram-web-app.js`; `telegram/init.ts` вызывает `init()`/`retrieveRawInitData` SDK и `initializeTelegramUi()` (`telegram/telegram.ts`): `ready()`, `expand()`, `disableVerticalSwipes()`, `setHeaderColor`/`setBackgroundColor`.
- **Получение/проверка `initData`:** сырой `initData` уходит в заголовке `X-Telegram-Init-Data`; сервер валидирует HMAC с бот-токеном (`TelegramInitDataValidator`); при невалидности — 401.
- **Сессия:** JWT/сессии нет — `initData` проверяется на каждом запросе (см. `docs/DECISIONS.md` ADR-006). Текущий пользователь — `CurrentUser`/`ICurrentUser`.
- **SDK-возможности:** тема через CSS (`--tg-theme-*` переопределены своей тёмной палитрой), haptics (`haptic`), нативный `BackButton` (`useBackButton`), `MainButton` в форме операции (`useMainButton`), `openTelegramLink` для шаринга, чтение `start_param` для приёма шаренных блюд.
- **Бот:** `TelegramController` принимает webhook (`X-Telegram-Bot-Api-Secret-Token`), режимы «Чек»/«Еда» (`IChatModeStore`, in-memory), скачивание файлов (`ITelegramBot.DownloadFileAsync`), уведомления о результате (`sendMessage`), команды `/start`, `/receipt`, `/food`. Голосовые сообщения — вежливый отказ.
- **Deep links:** `t.me/<bot>?startapp=fd-<token>` для шаринга блюд.

## 8. Запуск, проверка и развёртывание

- **Локально:** backend — `dotnet run --project src/Api` (нужен PostgreSQL, строка подключения `ConnectionStrings:Default`); frontend — `npm run dev` (Vite-прокси `/api` → `localhost:5177`). Docker — `docker compose up -d --build`.
- **Сервисы:** `db` (Postgres), `api` (.NET), `proxy` (Caddy). На боевом VPS `proxy` отключён, используется системный nginx (`deploy/docker-compose.server.yml`, `docs/SERVER.md`).
- **Переменные окружения (имена из `.env.example`):** `DOMAIN`, `BOT_TOKEN`, `OPENCODE_GO_KEY`, `AI_MODEL`, `AI_TEXT_MODEL`, `AI_CONFIDENCE_THRESHOLD`, `DB_PASSWORD`, `INIT_DATA_TTL`, `PUBLIC_BASE_URL`, `TELEGRAM_WEBHOOK_SECRET`, `BOT_USERNAME`. Значения не приводятся.
- **Тесты:** xUnit + EF Core InMemory, `70 [Fact]` (0 `[Theory]`), `TestDb.cs`; покрывают валидатор `initData`, сервисы (users/transactions/reports/receipts/food/saved dishes/food shares/statements) и процессоры (receipt/food/statement). Запуск: `dotnet test`. **Интеграционные тесты (Testcontainers) и фронтенд-тесты отсутствуют.**
- **CI/CD:** в репозитории **не найдено** (нет `.github/`). Развёртывание — вручную: `git pull` на сервере, `docker compose build api && up -d`, сборка фронта в контейнере `node:22-alpine` и копирование `web/dist` в каталог статики (`docs/SERVER.md`).
- **Логи/ошибки:** `ExceptionHandlingMiddleware` → `ProblemDetails`; логирование `x-logging` (json-file, ротация) в `docker-compose.yml`; для Telegram-клиента отключено info-логирование URL с токеном (в `appsettings.json`). Метрик/мониторинга не найдено.

## 9. Ограничения и незавершённые части

- **Автокатегоризация (этап 4) не реализована:** есть `CategoryRule`, `category_rules`, но нет сервиса/контроллера (в PLAN отмечено `[ ]`).
- **Этап 6 (расширенные отчёты) и этап 7 (эксплуатация: бэкапы, TTL файлов, лимиты на диске) не реализованы** (`docs/PLAN.md`).
- **Расхождения документации и кода:**
  - `docs/ARCHITECTURE.md` (поток авторизации) упоминает выдачу JWT/сессии — в коде JWT нет, используется `initData` на каждом запросе (ADR-006).
  - `docs/STACK.md` указывает Caddy как reverse-proxy; на боевом — системный nginx (`docs/SERVER.md`).
  - «OCR-слой» из ТЗ как отдельный компонент не найден (распознаёт vision-модель).
- **Не проверено:** интеграционные тесты с реальной БД; фактический UI на iOS/Android; устойчивость чанкинга на всех форматах выписок; работа при пустом `OPENCODE_GO_KEY` (есть заглушки, поведение не тестировалось вживую).
- **Технические ограничения:** режим чата бота хранится в памяти (`IChatModeStore`), сбрасывается при рестарте; `initData` не кэшируется; фронтенд-роутинг без библиотеки; длинные списки полагаются на `content-visibility`.
- Значения `TODO`/`FIXME`: в коде не обнаружено (кроме отмеченных «отложенных» пунктов в `docs/PLAN.md`).

## 10. Карта важных файлов

| Путь | Ответственность | Почему важен |
|---|---|---|
| `AGENTS.md` | Правила работы, команды | Точка входа для агента |
| `TZ_finance_food_tracker_miniapp.md` | Исходное ТЗ | Источник требований |
| `docs/PLAN.md` | Этапы и статусы | Текущее состояние |
| `docs/DECISIONS.md` | ADR и открытые вопросы | Обоснования решений |
| `docs/ARCHITECTURE.md`, `docs/STACK.md`, `docs/SERVER.md` | Архитектура/стек/деплой | Контекст инфраструктуры |
| `api/src/Application/Receipts/ReceiptService.cs`, `ReceiptProcessor.cs` | Чеки: загрузка, матчинг, проведение | Ключевой финансовый сценарий |
| `api/src/Application/Statements/*` | Импорт выписок | Новый крупный модуль |
| `api/src/Application/FoodLogs/*`, `SavedDishes/*`, `FoodShares/*` | Питание, избранное, шаринг | Продуктовый блок «Питание» |
| `api/src/Infrastructure/Analysis/OpenCodeGoVisionClient.cs` | Единый AI-клиент | Вся интеграция с AI |
| `api/src/Api/Middleware/TelegramInitDataMiddleware.cs` | Авторизация запросов | Безопасность |
| `api/src/Api/Controllers/TelegramController.cs` | Бот | Второй интерфейс |
| `web/src/App.tsx`, `screens/*`, `components/*` | UI/навигация | Состояние фронтенда |
| `docker-compose.yml`, `deploy/docker-compose.server.yml`, `Caddyfile` | Оркестрация | Развёртывание |

## 11. Неизвестное и вопросы владельцу

Из кода достоверно получить нельзя:
- **Продуктовые цели** и метрики успеха приложения.
- **Реальная аудитория**: количество и профиль пользователей (в коде только задел под мультипользовательность).
- **Приоритеты владельца** среди незавершённых этапов (автокатегоризация, отчёты, эксплуатация).
- **Ограничения по срокам и бюджету** (влияет на выбор «сделать самому vs интеграция»).
- **Требования к приватности/ретеншену** данных выписок и фото (ТЗ их не детализирует).
- **Планы монетизации** (упоминаются в общем виде только во внешней скилл-документации, не в проекте).
- **Целевые устройства/языки** и требования доступности сверх реализованного.
- **Ожидаемое поведение бота** при голосовых сообщениях (сейчас отказ — уточнить, нужен ли STT).
