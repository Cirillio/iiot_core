# IIoT Web API — справочник

Базовый адрес: `http://localhost:8080`. Интерактивная документация — Swagger UI
по `/swagger`.

**Соглашения:**

- Все URL в нижнем регистре (`LowercaseUrls`).
- Enum'ы сериализуются в **SCREAMING_SNAKE_CASE** (`INPUT_REGISTER`,
  `BIG_ENDIAN`, `ANALOG_RAW`, `SUCCESS`).
- Ошибки — формат RFC 7807 `ProblemDetails` (`GlobalExceptionMiddleware`):
  `{ status, title, detail, instance }`.
- Время — ISO 8601 (UTC).

---

## Connections — `/api/connections`

Физические сокеты Modbus (ip:port). Несколько устройств делят одно соединение.

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `GET` | `/api/connections` | Список соединений. |
| `GET` | `/api/connections/{id}` | Соединение по ID. |
| `POST` | `/api/connections` | Создать. Тело: `{ ipAddress, port, description? }`. |
| `PUT` | `/api/connections/{id}` | Обновить. |
| `DELETE` | `/api/connections/{id}` | Удалить (запрещено при наличии устройств — FK `RESTRICT`). |

---

## Devices — `/api/devices`

Логические устройства (Modbus slave) на соединении.

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `GET` | `/api/devices` | Список устройств (с preview тегов и `totalTags`). |
| `GET` | `/api/devices/{id}` | Устройство с полным списком тегов. |
| `POST` | `/api/devices` | Создать. Тело: `{ name, connectionId, slaveId, useGroupPolling, maxRegisterSpan, isActive, maxBitSpan? }`. |
| `PUT` | `/api/devices/{id}` | Обновить. |
| `DELETE` | `/api/devices/{id}` | Удалить. |

`isActive` — намерение оператора (опрашивать ли). Рантайм-доступность
(`isOnline`, `lastSeen`, `lastConnError`) пишет Collector.

---

## Tags — `/api/tags`

Точки опроса (датчики/каналы) устройства.

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `GET` | `/api/tags` | Все теги. |
| `GET` | `/api/tags/{id}` | Тег по ID. |
| `GET` | `/api/tags/device/{deviceId}` | Теги устройства. |
| `POST` | `/api/tags` | Создать (см. поля ниже). |
| `PUT` | `/api/tags/{id}` | Обновить (полная замена). |
| `DELETE` | `/api/tags/{id}` | Удалить. |

**Ключевые поля тега:**

- `dataType` — `ANALOG_RAW` (линейная интерполяция АЦП), `ANALOG_PHYSICAL`
  (готовая величина с прибора), `DIGITAL` (0/1).
- `registerType` — `INPUT_REGISTER`, `HOLDING_REGISTER`, `DISCRETE_INPUT`,
  `COIL`. Запись возможна только в `COIL` и `HOLDING_REGISTER`.
- `rawDataType` — `INT16`, `UINT16`, `INT32`, `UINT32`, `FLOAT32`, `FLOAT64`.
- `endianness` — `BIG_ENDIAN`, `LITTLE_ENDIAN`, `WORD_SWAP`, `BYTE_WORD_SWAP`.
- `inputMin/Max`, `outputMin/Max`, `offsetVal` — параметры масштабирования.
- `deadbandThreshold` — индивидуальный порог записи (null → глобальный).
- `uiConfig` — JSON-строка настроек отображения (цвет, пороги, метки).

Изменение конфигурации шлёт SignalR-событие `ConfigUpdated` и (через тег)
триггерит hot-reload в Collector.

---

## Metrics — `/api/metrics`

Временные ряды показаний.

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `GET` | `/api/metrics/history?tagId=&from=&to=` | История тега. Авто-выбор сырья vs `metrics_hourly`; равномерный `time_bucket`. Ответ: `[[ts_ms, value], ...]`. |
| `GET` | `/api/metrics/latest` | Последнее показание по каждому тегу: `[{ tagId, time, rawValue, value }]`. |
| `GET` | `/api/metrics/raw?tagId=&from=&to=&page=&pageSize=` | Постраничная выборка сырых метрик (новые сверху). |

**`/raw`** — все фильтры опциональны. `page` ≥ 1 (по умолч. 1),
`pageSize` 1..500 (по умолч. 50). Ответ — `PagedResult`:

```json
{
  "items": [
    { "tagId": 1, "tagName": "Boiler T", "unit": "°C",
      "time": "2026-06-06T10:00:00Z", "rawValue": 32768, "value": 50.0 }
  ],
  "total": 12345,
  "page": 1,
  "pageSize": 50
}
```

---

## System — `/api/system`

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `GET` | `/api/system/config` | Глобальная конфигурация (`system_config`). |
| `PUT` | `/api/system/config` | Обновить конфиг → `pg_notify 'config_changed'` → hot-reload Collector. |
| `GET` | `/api/system/status` | Пульс сервисов (`WebGateway`, `ModbusCollector`) из `system_status`. |

`system_config`: `rawRetentionDays`, `aggRetentionDays`, `pollingIntervalMs`,
`configReloadIntervalSec`, `healthCheckIntervalSec`, `deadbandThreshold`,
`dataHeartbeatSec`.

Изменение `rawRetentionDays` / `aggRetentionDays` через `PUT /api/system/config`
триггером `trg_apply_retention` пере-применяет политики хранения TimescaleDB:
сырьё (`metrics`) дропается после `rawRetentionDays`, часовые агрегаты
(`metrics_hourly`) — после `aggRetentionDays`. Изменение применяется на лету.

---

## Supervisory Control — `/api/v1/control`

| Метод | Путь | Описание |
| :--- | :--- | :--- |
| `POST` | `/api/v1/control/write` | Зарегистрировать асинхронную команду записи. |

**Тело:** `{ tagId, value, operatorId }` (для Coil: `1.0`=ON, `0.0`=OFF).

**Логика:**
1. Резолв тега (404, если не найден).
2. Запрет записи в Read-Only регистры (`DISCRETE_INPUT`/`INPUT_REGISTER`) → 400.
3. Проверка статуса Collector (если Offline → 503).
4. `INSERT device_commands (PENDING)` → `pg_notify 'control_commands'`.

**Ответ `202 Accepted`** — объект команды; клиент отслеживает исполнение по `id`
через канал `command_status_changed` (ControlHub).

| Код | Условие |
| :--- | :--- |
| `202` | Команда принята. |
| `400` | Запись в read-only регистр. |
| `404` | Тег не найден. |
| `503` | Collector недоступен. |

---

## SignalR-хабы

### `/hubs/metrics` (MonitoringHub)

Серверные события (server → client):

| Событие | Payload | Назначение |
| :--- | :--- | :--- |
| `ReceiveMetrics` | `json: string` — `{ time, tagId, rawValue, value }` | Live-метрики (ретрансляция канала `metrics_realtime`). |
| `ConfigUpdated` | `(entityType, entityId)` — тип: `DEVICE`/`TAG`/`CONNECTION` | Изменение конфигурации. |
| `SystemAlert` | `(message, level)` — уровень: `INFO`/`WARNING`/`CRITICAL` | Системные алерты. |

### `/hubs/control` (ControlHub)

| Событие | Payload | Назначение |
| :--- | :--- | :--- |
| `CommandStatusChanged` | `json: string` — `{ commandId, status, errorMessage }` | Жизненный цикл команды (ретрансляция `command_status_changed`). Фронт фильтрует по `commandId`. |

`status` команды: `PENDING` → `PROCESSING` → `SUCCESS` / `FAILED`.

---

## Каналы `pg_notify` (внутренняя шина)

| Канал | Триггер-источник | Слушатель |
| :--- | :--- | :--- |
| `metrics_realtime` | `INSERT metrics` | `MetricsObserverService` (Web API) |
| `control_commands` | `INSERT device_commands` | Collector |
| `command_status_changed` | `UPDATE device_commands.status` | `CommandObserverService` (Web API) |
| `config_changed` | `INSERT/UPDATE system_config` | Collector |
