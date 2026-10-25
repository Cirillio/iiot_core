# IIoT Monitoring & Supervisory Control

Промышленная система сбора телеметрии и супервизорного управления по протоколу
**Modbus TCP**. Опрашивает устройства, нормализует и хранит временные ряды,
транслирует показания в реальном времени и принимает команды записи в
исполнительные механизмы (Coil / Holding Register).

Стек: **.NET 10** (Collector + Web API), **PostgreSQL 15 + TimescaleDB**,
**Nuxt 4 / Vue 3.5** (админ-панель), Python (симулятор оборудования).

---

## Подсистемы

| Подсистема | Проект | Роль |
| :--- | :--- | :--- |
| **Acquisition** (сбор) | `IIoT.Collector` | Фоновый Worker: цикл опроса Modbus, десериализация регистров, масштабирование, deadband, буферизация в SQLite при потере связи с БД. |
| **Web API / Gateway** | `IIoT.WebApi` | REST для конфигурации и истории + SignalR для live-данных и обратной связи по командам. |
| **Общие модели** | `IIoT.Shared` | Доменные модели и enum'ы, разделяемые между Collector и Web API. |
| **Admin HMI** | `iiot_admin` | Nuxt-панель: дашборд, аналитика, таблица сырых данных, управление устройствами/тегами, АРМ супервизорного управления. |
| **База данных** | `iiot_init.sql` | TimescaleDB: гипертаблица метрик, continuous aggregate, триггеры `pg_notify` как шина событий. |
| **Симулятор** | `sim/` | Эмулятор Modbus-устройства для разработки без реального железа. |

---

## Архитектура

### Шина событий: PostgreSQL `LISTEN/NOTIFY`

Сервисы не общаются напрямую — БД выступает брокером событий. Триггеры на
таблицах публикуют уведомления, фоновые службы их слушают. Это убирает polling
и развязывает Collector и Web API.

| Канал | Источник (триггер) | Потребитель | Назначение |
| :--- | :--- | :--- | :--- |
| `metrics_realtime` | `INSERT` в `metrics` | Web API → SignalR | Live-трансляция показаний в UI. |
| `control_commands` | `INSERT` в `device_commands` | Collector | Пинг о новой команде записи (несёт только UUID). |
| `command_status_changed` | `UPDATE status` в `device_commands` | Web API → SignalR | Обратная связь о жизненном цикле команды. |
| `config_changed` | `INSERT/UPDATE` в `system_config` | Collector | Hot-reload параметров опроса без перезапуска. |

### Поток сбора (Acquisition)

```
Modbus-устройство ─(TCP)→ Collector
   ReadRegisters → десериализация (endianness/тип) → масштабирование+deadband
   → INSERT в metrics ─(trigger pg_notify 'metrics_realtime')→ Web API
   → SignalR /hubs/metrics → админ-панель (live)
```

При недоступности PostgreSQL Collector складывает метрики в локальный SQLite
(`buffer.db`) и дозаливает их после восстановления связи.

### Поток управления (Supervisory Control)

```
Админ-панель ─POST /api/v1/control/write→ Web API
   валидация прав записи (только Coil/Holding) + проверка статуса Collector
   → INSERT device_commands (PENDING) ─(pg_notify 'control_commands')→ Collector
   → Modbus Write → UPDATE статуса (SUCCESS/FAILED)
   ─(pg_notify 'command_status_changed')→ Web API → SignalR /hubs/control → UI
```

---

## Структура репозитория

```
IIoT/
├── IIoT.Collector/      # .NET Worker — опрос Modbus, обработка, буферизация
├── IIoT.WebApi/         # .NET Web API — REST + SignalR-хабы
├── IIoT.Shared/         # Общие модели и enum'ы
├── iiot_admin/          # Nuxt 4 админ-панель (HMI)
├── sim/                 # Python-симулятор Modbus-устройства
├── iiot_init.sql        # Схема БД: таблицы, гипертаблица, триггеры, seed
├── docker-compose.yml   # Оркестрация db / client / sim / gateway / cloudflared
├── Makefile             # Обёртки над docker compose
└── Docs/API.md          # Справочник REST-эндпоинтов и SignalR-хабов
```

---

## Запуск

Требуется **Docker** и **Make** (или Docker CLI). Все сервисы (включая админ-панель `iiot_admin`) оркестрируются через единый `docker-compose.yml`.

### Быстрый запуск всего стека (1 команда)

```bash
cp .env.example .env    # если файл .env ещё не создан
make up                 # сборка и запуск всех сервисов (db, client, sim, gateway, admin)
make status             # проверка статуса контейнеров
```

После старта доступны:
- Админ-панель (HMI): `http://localhost:3000`
- REST API / Swagger: `http://localhost:8080/swagger`
- Modbus TCP симулятор: `localhost:5020`

### Полезные команды Make

```bash
make logs         # логи всех контейнеров
make logs-client  # логи цикла опроса Modbus
make logs-web     # логи Web API и SignalR
make logs-admin   # логи веб-интерфейса
make down         # корректная остановка без потери данных
make reset-db     # сброс БД (down -v) для переинициализации схемы
```

### Локальная разработка фронтенда (без Docker для HMI)

```bash
cd iiot_admin
bun install
bun dev           # http://localhost:3000 с горячей перезагрузкой (HMR)
```

Адрес API конфигурируется переменной `NUXT_PUBLIC_API_BASE` (по умолчанию `http://localhost:8080`).

---

## Порты и сервисы

| Сервис | Контейнер | Порт | Описание |
| :--- | :--- | :--- | :--- |
| **Admin HMI** | `modbus_admin` | `3000` | Веб-интерфейс Nuxt 4 (production Node-сервер). |
| **Web API** | `modbus_web_gateway` | `8080` | REST + SignalR + Swagger UI (`/swagger`). |
| **Collector** | `modbus_client` | — | Опрос Modbus, нормализация, запись в БД и буфер. |
| **PostgreSQL** | `adam_db` | `5432` | TimescaleDB хранилище телеметрии и событий. |
| **Modbus Sim** | `modbus_sim` | `5020` | Эмулятор Modbus TCP оборудования. |
| **Cloudflare** | `cloudflared` | — | Туннель для безопасного удаленного доступа. |

**Переменные окружения** (`.env`):

- `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` — реквизиты PostgreSQL.
- `NUXT_PUBLIC_API_BASE` — адрес Web API для фронтенда (клиентский браузер).
- `CORS_ALLOWED_ORIGINS` — список разрешённых origins для CORS в Web API (через запятую).
- `CLOUDFLARE_TUNNEL_TOKEN` — токен Cloudflare Zero Trust туннеля.

---

## Документация и безопасность

- [Безопасность и модель контроля доступа](Docs/Security_and_Access_Control.md) — модель доверия, риски эндпоинтов управления и рекомендации по защите периметра.
- [Справочник API и протоколов](Docs/API.md) — спецификация REST-эндпоинтов, хабов SignalR и структур данных.
- [Инфраструктура и траблшутинг](Docs/Эксплуатация%20(Docker%20и%20Cloudflare).md) — решение инцидентов и сетевые настройки.

