# Техническое задание
## Telegram Mini App — учёт финансов и питания с AI-анализом чеков и фото блюд

---

## 1. Общее описание

Telegram Mini App для личного учёта доходов и расходов, распознавания чеков из магазинов и анализа фото блюд для оценки калорийности и состава питания. Пользователь работает через Telegram, не устанавливая отдельное приложение. Backend и хранилище разворачиваются в Docker Compose на своём сервере.

### 1.1 Цели проекта

- Учитывать все приходы и расходы денег в одном месте.
- Автоматически разбирать чеки на позиции и категории через AI, без ручного ввода каждой строки.
- Анализировать фото блюд и давать оценку калорийности и БЖУ.
- Строить отчёты: расходы по категориям, по месяцам, по магазинам, дневник питания.
- Работать экономно по ресурсам сервера (минимум RAM/CPU, контролируемый размер логов и данных).

### 1.2 Не входит в объём (v1)

- Многопользовательские команды/семейный бюджет (только личный аккаунт, но модель данных должна закладывать `UserId` с самого начала).
- Синхронизация с банковскими API.
- Мобильные push-уведомления вне Telegram.
- Учёт нескольких валют одновременно (только BYN на старте, с полем `Currency` в БД под расширение).

---

## 2. Технологический стек

| Слой | Технология | Комментарий |
|---|---|---|
| Frontend (Mini App) | React + `@telegram-apps/sdk` (или `telegram-web-app.js`) | Легкий SPA, тема через `themeParams` |
| Backend API | ASP.NET Core (.NET 8/9), минимальный API или контроллеры | Соответствует твоему стеку |
| БД | PostgreSQL | Реляционные данные, EF Core |
| Кэш/очередь (опц.) | Redis | Только если нужна очередь на анализ фото — иначе не подключать в v1, чтобы не грузить сервер |
| Хранилище файлов | Локальная volume-папка или MinIO (S3-совместимо) | На старте — volume, MinIO при росте объёма |
| AI-анализ | OpenCode Go API (vision-модель) + OCR-слой | Через `IReceiptAnalyzer` / `IFoodImageAnalyzer` |
| Реверс-прокси | Nginx или Caddy (лёгкий) | TLS, роутинг `/api` и статики |
| Оркестрация | Docker Compose | Единый `docker-compose.yml` на сервере |

**Требование:** backend должен обращаться к AI-провайдеру через абстракцию (`IReceiptAnalyzer`, `IFoodImageAnalyzer`), чтобы провайдера можно было заменить без изменения бизнес-логики.

---

## 3. Архитектура системы

```text
Telegram Client
      |
Telegram Mini App (React, статика через Nginx)
      |  (initData, HTTPS)
ASP.NET Core API  ---->  PostgreSQL
      |                       |
      |                  Volume: pg_data
      |
      |----> Volume: uploads (чеки, фото блюд)
      |
      |----> OpenCode Go API (внешний, HTTPS)
```

Все контейнеры — в одной Docker-сети `internal`, наружу торчит только reverse-proxy (порты 80/443).

---

## 4. Модель данных (PostgreSQL)

### 4.1 Основные таблицы

**users**
- id (uuid, PK)
- telegram_id (bigint, unique)
- username
- created_at

**accounts** (счета/кошельки, на будущее — можно на старте один счёт по умолчанию)
- id (uuid, PK)
- user_id (FK)
- name
- currency (varchar, default 'BYN')

**categories**
- id (uuid, PK)
- user_id (FK, nullable — системные категории общие)
- name
- type (income / expense)
- parent_id (nullable, для подкатегорий)
- is_system (bool)

**transactions**
- id (uuid, PK)
- user_id (FK)
- account_id (FK)
- category_id (FK, nullable до классификации)
- type (income / expense)
- amount (numeric(12,2))
- currency
- occurred_at (timestamp)
- source (manual / receipt / ai_suggested)
- comment (text, nullable)
- receipt_id (FK, nullable)
- created_at

**receipts**
- id (uuid, PK)
- user_id (FK)
- image_path
- merchant_name (nullable)
- purchase_date (nullable)
- total_amount (nullable)
- raw_ocr_text (text, nullable)
- ai_raw_response (jsonb, nullable)
- status (pending / processed / needs_review / failed)
- confidence (numeric, nullable)
- created_at

**receipt_items**
- id (uuid, PK)
- receipt_id (FK)
- name
- quantity
- unit_price
- total_price
- category_id (FK, nullable)
- confidence (numeric, nullable)

**food_logs**
- id (uuid, PK)
- user_id (FK)
- image_path
- dish_name (nullable)
- calories_min / calories_max (nullable)
- protein_g / fat_g / carbs_g (nullable, диапазоны или средние)
- eaten_at (timestamp)
- ai_raw_response (jsonb, nullable)
- status (pending / processed / needs_review / failed)
- created_at

**category_rules** (обучаемые правила классификации)
- id (uuid, PK)
- user_id (FK)
- match_pattern (например, "евроопт", "steam")
- category_id (FK)
- created_at

### 4.2 Требования к данным

- Все денежные суммы — `numeric`, не `float`.
- Оригиналы изображений хранить обязательно (для повторного анализа).
- `ai_raw_response` хранить как `jsonb` для отладки и повторной обработки без переспроса пользователя.

---

## 5. Основные сценарии (User Stories)

1. Пользователь открывает Mini App через кнопку в боте, авторизация происходит автоматически через `initData`.
2. Пользователь фотографирует чек в магазине → отправляет в приложение → видит разобранные позиции с категориями → подтверждает или правит.
3. Пользователь вручную добавляет доход (зарплата) или расход без фото.
4. Пользователь фотографирует блюдо → получает оценку калорий/БЖУ с диапазоном → может скорректировать вес/состав.
5. Пользователь открывает дашборд: траты по категориям за месяц, топ магазинов, дневник питания за неделю.
6. Приложение запоминает, что «Евроопт» = категория «Продукты», и в следующий раз применяет это автоматически.

---

## 6. Backend API (ключевые эндпоинты)

Все запросы проходят проверку `initData` (HMAC-подпись бот-токеном) на каждый запрос [web:24].

| Метод | Путь | Назначение |
|---|---|---|
| POST | /api/auth/telegram | Валидация initData, выдача сессии/JWT |
| GET | /api/transactions | Список операций с фильтрами (период, категория) |
| POST | /api/transactions | Ручное создание операции |
| PATCH | /api/transactions/{id} | Правка категории/суммы |
| POST | /api/receipts | Загрузка фото чека, постановка на анализ |
| GET | /api/receipts/{id} | Статус и результат разбора чека |
| PATCH | /api/receipts/{id}/items/{itemId} | Правка позиции чека |
| POST | /api/food-logs | Загрузка фото блюда |
| GET | /api/food-logs/{id} | Результат анализа блюда |
| GET | /api/reports/summary | Свод по категориям/периодам |
| GET | /api/categories | Список категорий |
| POST | /api/category-rules | Создание правила автокатегоризации |

Загрузка файлов — ограничение размера (например, до 8 МБ на изображение), сжатие на клиенте перед отправкой, чтобы не гонять большие файлы через сервер и не раздувать volume.

---

## 7. Требования к AI-анализу

- Vision-запросы идут через отдельный сервис/класс, изолированный от контроллеров.
- Ответ модели принимается строго в JSON-формате (structured output), без свободного текста.
- Backend валидирует: сумма позиций ≈ итог чека (допуск на округление), обязательные поля не пустые.
- Если `confidence` ниже порога — статус `needs_review`, пользователь видит пометку и правит вручную.
- Для питания — всегда диапазон калорий/БЖУ, а не одно точное число, с пометкой "оценка".
- Кэшировать/логировать сырые ответы AI (`ai_raw_response`) для отладки промптов без повторной траты запроса.

---

## 8. Docker: контейнеры и требования к ресурсам

### 8.1 Состав контейнеров

| Сервис | Образ | Роль |
|---|---|---|
| proxy | nginx:alpine или caddy:alpine | TLS, роутинг, отдача статики фронта |
| api | собственный образ (.NET, multi-stage build на `aspnet:8-alpine`) | Backend API |
| db | postgres:16-alpine | Хранилище данных |
| (опц.) minio | minio/minio | Хранилище файлов, если volume перестанет хватать |

### 8.2 Требования по весу и ресурсам

- Использовать **alpine**-варианты образов везде, где возможно (`aspnet:8-alpine`, `postgres:16-alpine`, `nginx:alpine`) — меньше вес образа, меньше поверхность атаки.
- Multi-stage Dockerfile для .NET: сборка в `sdk`-образе, финальный слой — только `aspnet-alpine` runtime + опубликованные бинарники, без SDK.
- Файлы чеков/фото хранить в volume с ограничением через внешний мониторинг диска (или периодическую очистку старых `pending`/`failed` записей с истёкшим TTL).

### 8.3 Ограничение ресурсов (CPU/RAM) в Compose

Для Docker Compose (не Swarm) ресурсы ограничиваются через `mem_limit`/`cpus` на уровне сервиса [web:19]:

```yaml
services:
  api:
    mem_limit: 512m
    cpus: 1.0
  db:
    mem_limit: 512m
    cpus: 1.0
  proxy:
    mem_limit: 128m
    cpus: 0.5
```

Для PostgreSQL на маленьком сервере дополнительно настроить `shared_buffers`, `max_connections` под доступную память — не оставлять дефолтные значения, рассчитанные на больший сервер.

### 8.4 Ограничение логов Docker

По умолчанию `json-file` драйвер не ограничен и может занять весь диск [web:13]. Обязательно задать лимиты на каждый сервис через `logging.options` (`max-size`, `max-file`, `compress`) [web:15][web:18]:

```yaml
x-logging: &default-logging
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
    compress: "true"

services:
  api:
    logging: *default-logging
  db:
    logging: *default-logging
  proxy:
    logging: *default-logging
```

Это ограничивает каждый сервис максимум ~30 МБ логов (10 МБ × 3 файла) [web:20][web:23]. Дополнительно рекомендуется задать те же лимиты глобально в `/etc/docker/daemon.json`, чтобы любые контейнеры без явной секции `logging` тоже не разрастались [web:15][web:25]:

```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3",
    "compress": "true"
  }
}
```

### 8.5 Итоговый пример docker-compose.yml (каркас)

```yaml
version: "3.8"

x-logging: &default-logging
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
    compress: "true"

services:
  proxy:
    image: caddy:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
    depends_on:
      - api
    mem_limit: 128m
    cpus: 0.5
    logging: *default-logging
    restart: unless-stopped

  api:
    build: ./api
    environment:
      - ConnectionStrings__Default=Host=db;Database=finance;Username=app;Password=${DB_PASSWORD}
      - OpenCodeGo__ApiKey=${OPENCODE_GO_KEY}
    volumes:
      - uploads:/app/uploads
    depends_on:
      - db
    mem_limit: 512m
    cpus: 1.0
    logging: *default-logging
    restart: unless-stopped

  db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_DB=finance
      - POSTGRES_USER=app
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - pg_data:/var/lib/postgresql/data
    mem_limit: 512m
    cpus: 1.0
    logging: *default-logging
    restart: unless-stopped

volumes:
  pg_data:
  uploads:
  caddy_data:
```

Итоговый суммарный лимит логов — не более ~90 МБ на все три сервиса, суммарный лимит RAM — около 1.1 ГБ, что подходит для небольшого VPS.

---

## 9. Безопасность

- Проверка `initData` строго на сервере через HMAC с бот-токеном на каждый запрос — клиенту не доверять [web:24].
- Секреты (бот-токен, ключ OpenCode Go, пароль БД) — только через переменные окружения / `.env`, не в репозитории.
- Ограничение размера загружаемых файлов на уровне reverse-proxy и API.
- HTTPS обязателен (Telegram Mini App требует HTTPS для WebView).
- Регулярные бэкапы volume `pg_data` (минимум раз в сутки, ротация бэкапов).

---

## 10. Нефункциональные требования

- Время отклика API на CRUD-операции — не более 300 мс на типовом VPS.
- Анализ чека/фото блюда — асинхронный процесс: пользователь сразу видит статус "в обработке", результат подгружается по готовности (polling или веб-сокет/long-polling).
- Приложение должно продолжать работать (ручной ввод, просмотр отчётов) даже если AI-провайдер временно недоступен.
- Совокупный объём Docker-образов проекта (без учёта Postgres) — стремиться к суммарному весу до ~300–400 МБ.

---

## 11. План поэтапной реализации (MVP → расширение)

1. **Этап 1** — каркас: Telegram-авторизация, ручной ввод доходов/расходов, категории, простой отчёт по месяцу.
2. **Этап 2** — загрузка фото чека, хранение оригинала, статус обработки (пока без AI — заглушка/ручной разбор).
3. **Этап 3** — подключение AI-анализа чеков (OpenCode Go + OCR), структурированный JSON, валидация суммы.
4. **Этап 4** — правила автокатегоризации, обучение на правках пользователя.
5. **Этап 5** — фото блюд, оценка калорий/БЖУ с диапазонами.
6. **Этап 6** — расширенные отчёты: тренды, топ магазинов, связка "траты на еду vs калории".
7. **Этап 7** — оптимизация: лимиты логов/ресурсов, бэкапы, мониторинг диска, при необходимости переход uploads → MinIO.

---

## 12. Открытые вопросы для уточнения

- Нужен ли мультивалютный учёт сразу или BYN достаточно на первом этапе?
- Нужна ли история изменений операций (аудит правок) или достаточно текущего состояния?
- Как часто планируется анализировать фото блюд — по каждому приёму пищи или выборочно?
