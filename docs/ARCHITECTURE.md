# Архитектура

Описание системы, модели данных, API и пайплайна AI (по ТЗ §3–§8).

## Общая схема

```text
Telegram Client
      |
Telegram Mini App (React, статика через reverse-proxy)
      |  (initData, HTTPS)
ASP.NET Core API  ---->  PostgreSQL
      |                       |
      |                  Volume: pg_data
      |
      |----> Volume: uploads (чеки, фото блюд)
      |
      |----> OpenCode Go API (внешний, HTTPS)
```

Все контейнеры — в одной Docker-сети `internal`. Наружу — только reverse-proxy (80/443).

## Слои backend

```text
Api              — контроллеры/эндпоинты, DTO, авторизация, валидация входа
Application      — use-cases, оркестрация, интерфейсы (IReceiptAnalyzer, IFoodImageAnalyzer, IFileStorage)
Domain           — сущности, правила, enum'ы (TransactionType, ReceiptStatus…)
Infrastructure   — EF Core, Postgres, файловое хранилище, AI-клиенты, фоновые обработчики
```

Правила:
- Контроллеры не содержат бизнес-логики.
- AI-вызовы — только через интерфейсы Application.
- DTO отделены от EF-сущностей.

## Поток авторизации

1. Mini App получает `initData` от Telegram.
2. Клиент отправляет `initData` в `POST /api/auth/telegram`.
3. Backend проверяет HMAC c `BOT_TOKEN` (строго на сервере), парсит `user`, upsert в `users`.
4. Выдаётся JWT/сессия; далее запросы авторизуются, `initData` валидируется на каждом защищённом запросе.

## Поток анализа чека

```text
POST /api/receipts (multipart)
   -> сохранить оригинал в uploads, создать receipt(status=pending)
   -> фоновая задача: OCR + IReceiptAnalyzer (structured JSON)
   -> валидация: сумма позиций ≈ total_amount, обязательные поля, confidence
   -> receipt_items + receipt(status=processed | needs_review | failed)
   -> ai_raw_response (jsonb) сохранён
GET /api/receipts/{id}  (polling статуса)
   -> подтверждение: создание transactions (source=receipt)
```

## Поток анализа блюда

```text
POST /api/food-logs (multipart)
   -> сохранить оригинал, food_log(status=pending)
   -> IFoodImageAnalyzer -> диапазоны калорий/БЖУ + dish_name
   -> food_log(status=processed | needs_review | failed), ai_raw_response
GET /api/food-logs/{id} (polling)
   -> пользователь может скорректировать
```

## Модель данных (PostgreSQL)

### users
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| telegram_id | bigint unique | |
| username | text | |
| created_at | timestamp | |

### accounts
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK users | |
| name | text | |
| currency | varchar default 'BYN' | задел под мультивалютность |

### categories
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK nullable | null — системные общие |
| name | text | |
| type | income / expense | |
| parent_id | uuid nullable | подкатегории |
| is_system | bool | |

### transactions
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK | |
| account_id | uuid FK | |
| category_id | uuid FK nullable | до классификации |
| type | income / expense | |
| amount | numeric(12,2) | не float |
| currency | varchar | |
| occurred_at | timestamp | |
| source | manual / receipt / ai_suggested | |
| comment | text nullable | |
| receipt_id | uuid FK nullable | |
| created_at | timestamp | |

### receipts
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK | |
| image_path | text | оригинал обязателен |
| merchant_name | text nullable | |
| purchase_date | timestamp nullable | |
| total_amount | numeric nullable | |
| raw_ocr_text | text nullable | |
| ai_raw_response | jsonb nullable | отладка промптов |
| status | pending / processed / needs_review / failed | |
| confidence | numeric nullable | |
| created_at | timestamp | |

### receipt_items
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| receipt_id | uuid FK | |
| name | text | |
| quantity | numeric | |
| unit_price | numeric | |
| total_price | numeric | |
| category_id | uuid FK nullable | |
| confidence | numeric nullable | |

### food_logs
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK | |
| image_path | text | |
| dish_name | text nullable | |
| calories_min / calories_max | numeric nullable | диапазон |
| protein_g / fat_g / carbs_g | numeric nullable | диапазоны/средние |
| eaten_at | timestamp | |
| ai_raw_response | jsonb nullable | |
| status | pending / processed / needs_review / failed | |
| created_at | timestamp | |

### category_rules
| Поле | Тип | Примечание |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK | |
| match_pattern | text | напр. «евроопт», «steam» |
| category_id | uuid FK | |
| created_at | timestamp | |

## API (ключевые эндпоинты)

Все защищённые запросы проходят валидацию `initData` (HMAC с бот-токеном).

| Метод | Путь | Назначение |
|---|---|---|
| POST | `/api/auth/telegram` | Валидация `initData`, выдача сессии/JWT |
| GET | `/api/transactions` | Список операций с фильтрами (период, категория) |
| POST | `/api/transactions` | Ручное создание операции |
| PATCH | `/api/transactions/{id}` | Правка категории/суммы |
| POST | `/api/receipts` | Загрузка фото чека, постановка на анализ |
| GET | `/api/receipts/{id}` | Статус и результат разбора |
| PATCH | `/api/receipts/{id}/items/{itemId}` | Правка позиции чека |
| POST | `/api/food-logs` | Загрузка фото блюда |
| GET | `/api/food-logs/{id}` | Результат анализа блюда |
| GET | `/api/reports/summary` | Свод по категориям/периодам |
| GET | `/api/categories` | Список категорий |
| POST | `/api/category-rules` | Создание правила автокатегоризации |

Загрузка файлов: лимит до 8 МБ, сжатие на клиенте до отправки.

## Требования к AI-анализу

- Vision-запросы изолированы в сервисе/классе, не в контроллерах.
- Ответ строго JSON (structured output), без свободного текста.
- Валидация: сумма позиций ≈ итог чека (допуск на округление), обязательные поля.
- `confidence` ниже порога → `needs_review`, пользователь правит вручную.
- Питание — всегда диапазон, с пометкой «оценка».
- Сырые ответы AI сохраняются (`ai_raw_response`) для отладки промптов.

## Docker: контейнеры

| Сервис | Образ | Роль |
|---|---|---|
| proxy | caddy:alpine (или nginx:alpine) | TLS, роутинг, статика |
| api | собственный (.NET, multi-stage `aspnet:8-alpine`) | Backend API |
| db | postgres:16-alpine | Данные |
| (опц.) minio | minio/minio | Файлы при росте объёма |

См. `docs/STACK.md` — лимиты RAM/CPU и логи.

## Безопасность

- `initData` — HMAC на сервере на каждый запрос; клиенту не доверять.
- Секреты — env/`.env`, не в репозитории.
- Лимиты размера загрузок на proxy и API.
- HTTPS обязателен.
- Бэкап `pg_data` минимум раз в сутки с ротацией.

## Нефункциональные требования

- CRUD API ≤ 300 мс.
- Анализ — асинхронный (`pending` → результат по готовности).
- Работа без AI-провайдера.
- Образы без Postgres ≤ ~300–400 МБ.
