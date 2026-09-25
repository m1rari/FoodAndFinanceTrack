# Сервер: доступ и боевой деплой

Документ описывает размещение приложения на боевом VPS. **Секретов здесь нет** — токены, пароли и приватные ключи хранятся вне репозитория.

## 1. Доступ

| Параметр | Значение |
|---|---|
| Хост | `45.128.205.200` |
| Пользователь | `root` |
| Порт SSH | `22` |
| Домен сайта-соседа | `pinsk-elektrik.by` (не наш, не трогать) |
| Репозиторий на сервере | `/home/FoodTrack/FoodAndFinanceTrack` |
| Git remote | `https://github.com/m1rari/FoodAndFinanceTrack.git` |

Вход — только по SSH-ключу. Публичный ключ `opencode-deploy` добавлен в `/root/.ssh/authorized_keys`.

Приватный ключ **не хранится в репозитории**. Текущая копия на рабочей машине:

```
C:\Users\46D8~1\AppData\Local\Temp\opencode\fft_deploy
```

Это временная папка — перед её очисткой скопируйте ключ в постоянное место, например `%USERPROFILE%\.ssh\fft_deploy`.

Подключение:

```powershell
ssh -i "$env:USERPROFILE\.ssh\fft_deploy" root@45.128.205.200
```

По завершении работ ключ можно удалить из `/root/.ssh/authorized_keys`.

## 2. Архитектура размещения (важно!)

На сервере **уже работает посторонний сайт** `pinsk-elektrik.by` (Next.js, PM2-процесс `ValikLanding` на `localhost:3000`). Порты 80/443 заняты системным **nginx** — Caddy из базового `docker-compose.yml` на этом сервере отключён.

```text
Internet
   |
host nginx (80/443)  -- TLS (certbot / letsencrypt)
   |-- pinsk-elektrik.by          -> ValikLanding :3000     (чужой сайт)
   |-- 45-128-205-200.sslip.io    -> /var/www/foodtrack      (наш фронт)
   |                                 + /api/ -> 127.0.0.1:8090
   |
Docker:
   api  (aspnet:8-alpine)   published 127.0.0.1:8090 -> 8080
   db   (postgres:16-alpine) internal only
```

- `api` и `db` ограничены: 256m/0.5cpu и 320m/0.5cpu, Postgres ужат (`shared_buffers=64MB`, `max_connections=20`).
- Добавлен swap-файл 2 ГБ (`/swapfile2`) — нужен для сборки образов на VPS с 1 ГБ RAM.
- **Не трогать** PM2-процессы `ValikLanding` и `telegram-admin-bot`.

### Ключевые пути

| Что | Где |
|---|---|
| Репозиторий | `/home/FoodTrack/FoodAndFinanceTrack` |
| `.env` (не в git) | `/home/FoodTrack/FoodAndFinanceTrack/.env` |
| Server-override | `/home/FoodTrack/FoodAndFinanceTrack/deploy/docker-compose.server.yml` |
| nginx-сайт | `/etc/nginx/sites-available/foodtrack` (+ symlink в `sites-enabled`) |
| Статика фронта | `/var/www/foodtrack` |
| Smoke-скрипт | `/tmp/fft-smoke.sh` |

## 3. Переменные `.env` на сервере

Файл `/home/FoodTrack/FoodAndFinanceTrack/.env` (права `600`, в git не попадает):

```dotenv
DOMAIN=...
BOT_TOKEN=...          # токен бота, чей Mini App открывается
OPENCODE_GO_KEY=...    # пока пусто (AI — этапы 3/5)
DB_PASSWORD=...        # сгенерирован на сервере
INIT_DATA_TTL=24:00:00
```

Посмотреть, не выводя секреты целиком: `grep -E '^(DOMAIN|INIT_DATA_TTL)=' .env`.

## 4. Управление контейнерами

```bash
cd /home/FoodTrack/FoodAndFinanceTrack
export COMPOSE_FILE="docker-compose.yml:deploy/docker-compose.server.yml"

docker compose ps
docker compose logs -f api
docker compose up -d api db
docker compose restart api
docker compose down          # остановить только наш стек (db + api)
```

`COMPOSE_FILE` можно закрепить в `~/.bashrc`, либо всегда указывать `-f`.

## 5. Обновление после `git pull`

```bash
cd /home/FoodTrack/FoodAndFinanceTrack
git pull
export COMPOSE_FILE="docker-compose.yml:deploy/docker-compose.server.yml"

# Бэкенд (если менялся api/):
docker compose build api && docker compose up -d api

# Фронтенд (если менялся web/): собираем контейнером, выкладываем статику
docker run --rm -v "$PWD/web:/app" -w /app -e npm_config_cache=/tmp/npm-cache \
  node:22-alpine sh -c "npm ci --no-audit --no-fund && npm run build"
rm -rf /var/www/foodtrack/* && cp -r web/dist/. /var/www/foodtrack/
```

После копирования статики перезапуск nginx не нужен.

## 6. TLS и домены

- TLS выпускается через `certbot` (webroot `/var/www/html`), автопродление systemd-таймером.
- Текущий боевой адрес приложения (временный): `https://45-128-205-200.sslip.io` — публичный DNS `sslip.io` маппит имя на IP, сертификат Let's Encrypt на него уже выпущен.
- Планируемый адрес: `https://food.pinsk-elektrik.by` (нужна A-запись `food -> 45.128.205.200` в панели **cloudvps.by**, где размещена DNS-зона).
- Выпуск сертификата для поддомена и добавление 443-блока nginx (пример):

```bash
# после того как food.pinsk-elektrik.by начнёт резолвиться
certbot certonly --webroot -w /var/www/html -d food.pinsk-elektrik.by --non-interactive --agree-tos
# затем добавить server{ listen 443 ssl; server_name food.pinsk-elektrik.by; ... } в /etc/nginx/sites-available/foodtrack
nginx -t && systemctl reload nginx
```

## 7. Smoke-тест API

`/tmp/fft-smoke.sh` генерирует валидный `initData` из `BOT_TOKEN` и проверяет auth / создание операции / отчёт / 401 без заголовка.

```bash
bash /tmp/fft-smoke.sh "https://45-128-205-200.sslip.io"
```

## 8. Особенности окружения

- Node на хосте: `v20.20.2`, npm `10.8.2`. **npm на хосте падает** («Exit handler never called») → фронт собирать в контейнере `node:22-alpine`.
- `web/package-lock.json` не должен содержать внутренних registry-URL — только `https://registry.npmjs.org/` (иначе сборка на сервере падает с `ENOTFOUND`).
- `dotnet` на хосте отсутствует — API компилируется в Docker-образе.
- Docker: сервис `proxy` в базовом compose требует порты 80/443 — на этом сервере всегда отключается профилем `disabled`.

## 9. Статус

- [x] `api` + `db` подняты, миграции применены, БД healthy.
- [x] HTTPS через `45-128-205-200.sslip.io`, smoke-тест пройден (внутренний и внешний).
- [ ] DNS `food.pinsk-elektrik.by` опубликован не был (NS отдают NXDOMAIN) — переключение на реальный поддомен отложено.
- [ ] Ротация `BOT_TOKEN` и удаление SSH-ключа после тестов.
- [x] Тестовые данные smoke-прогонов (2 операции, пользователь `telegramId 123456789`) — удалить при желании.
