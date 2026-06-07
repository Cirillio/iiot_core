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

Требуется **Docker** и **Make**. Бэкенд-сервисы поднимаются через
`docker-compose`; админ-панель запускается отдельно (Nuxt dev/preview).

### Бэкенд (Collector + API + БД + симулятор)

```bash
make up           # сборка и запуск всех контейнеров в фоне
make logs-client  # логи опроса (ожидать "Data successfully saved to DB")
make down         # остановка без удаления данных
make reset-db     # сброс БД (down -v) — переинициализация свежим iiot_init.sql
```

Полный список команд: `make help`.

### Админ-панель

```bash
cd iiot_admin
bun install
bun dev           # http://localhost:3000
```

API-адрес настраивается через `runtimeConfig.public.apiBase`
(по умолчанию `http://localhost:8080`).

---

## Порты и конфигурация

| Сервис | Порт | Описание |
| :--- | :--- | :--- |
| Web API | `8080` | REST + SignalR + Swagger (`/swagger`). |
| PostgreSQL | `5432` | TimescaleDB. |
| Modbus-симулятор | `5020` | Эмулятор устройства. |
| Admin (dev) | `3000` | Nuxt dev-сервер. |

**Переменные окружения** (`docker-compose.yml`):

- `ConnectionStrings__AdamMonitoring` / `ConnectionStrings__ADAMDB` — строка
  подключения к PostgreSQL для Web API и Collector соответственно.
- `ModbusSettings__IpAddress` / `ModbusSettings__Port` — адрес опрашиваемого
  устройства (по умолчанию указывает на контейнер `sim`).
- `CLOUDFLARE_TUNNEL_TOKEN` (в `.env`) — токен Cloudflare-туннеля для внешнего
  доступа.

Параметры периода опроса, deadband и хранения данных хранятся в таблице
`system_config` и редактируются через раздел **Settings** админ-панели —
изменения применяются на лету через канал `config_changed`.

**Хранение данных (retention).** TimescaleDB сама дропает устаревшие чанки по
политикам, которые ставит триггер `trg_apply_retention` на `system_config`:
сырые метрики `metrics` живут `rawRetentionDays` (90 дней по умолчанию), часовые
агрегаты `metrics_hourly` — `aggRetentionDays` (5 лет). Правка этих полей в
Settings немедленно пере-применяет политики. Политики устанавливаются при
инициализации БД, поэтому на существующей БД (init-скрипт не перезапускается)
для их появления нужен `make reset-db` либо одно сохранение настроек в Settings
— любой `UPDATE` полей retention поднимает триггер.

---

## API

Полный справочник REST-эндпоинтов, SignalR-хабов и форматов payload —
в [Docs/API.md](Docs/API.md). Интерактивная документация доступна в Swagger UI
по адресу `http://localhost:8080/swagger`.
