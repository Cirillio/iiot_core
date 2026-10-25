# Эксплуатация: Docker, Cloudflare, troubleshooting

Журнал решённых инфраструктурных проблем + шпаргалка по командам. Записи историчны
(сохранены как «грабли и решения»); даты указывают, когда проблема разбиралась.

> Современный CLI — `docker compose` (с пробелом). Старый `docker-compose` (дефис) упоминается
> там, где проблема была именно в нём.

---

## 1. Журнал инцидентов и решений

### 1.1. Права доступа (Permission denied)
- **Проблема:** `PermissionError: [Errno 13] Permission denied` при обращении к Docker daemon.
- **Причина:** пользователь не в группе `docker` → нет доступа к `/var/run/docker.sock`.
- **Решение:** `sudo usermod -aG docker $USER` и перелогиниться (временно — `sudo` перед командами).

### 1.2. Привилегированные порты (Modbus 502)
- **Проблема:** симулятор требовал root / не стартовал.
- **Причина:** порты < 1024 (станд. Modbus 502) привилегированные в Linux.
- **Решение:** порт симулятора → **5020**; в `IIoT.Collector/appsettings.json` и compose прописан нестандартный порт.

### 1.3. Конфликт версий библиотек (Swagger/Swashbuckle)
- **Проблема:** WebApi падал с `System.TypeLoadException` на старте.
- **Причина:** пре-релизная `Swashbuckle.AspNetCore 7.2.0`, несовместимая с окружением .NET.
- **Решение:** даунгрейд до стабильной **6.5.0** в `IIoT.WebApi.csproj`.

### 1.4. Сетевая изоляция Docker (Connection refused / DNS)
- **Проблема:** collector не видел `sim`/`db`.
- **Причина:** `network_mode: "host"` у сервиса → теряется внутренний DNS Docker (имена `db`, `sim`).
- **Решение:** убран `network_mode: host`; строки подключения `127.0.0.1` → имена сервисов `db` / `sim`.

### 1.5. Отсутствие системных зависимостей (GSSAPI/Kerberos)
- **Проблема:** `Cannot load library libgssapi_krb5.so.2` при подключении к PostgreSQL.
- **Причина:** `Npgsql` тянет GSSAPI, в облегчённом Debian/Alpine библиотек нет.
- **Решение:** в `Dockerfile` добавить `RUN apt-get update && apt-get install -y libgssapi-krb5-2`.

### 1.6. «Зомби»-контейнеры (KeyError: 'ContainerConfig')
- **Проблема:** старый `docker-compose` (Python) падал при пересоздании контейнеров.
- **Причина:** несовместимость метаданных нового Docker Engine со старым парсером.
- **Решение:** ручное удаление: `docker stop <name> && docker rm <name>` (или перейти на `docker compose`).

### 1.7. Логирование (Serilog)
- **Проблема:** не выводились Debug-логи вопреки настройкам compose.
- **Причина:** уровень захардкожен в C# (`.MinimumLevel.Information()`).
- **Решение:** пакет `Serilog.Settings.Configuration` + `.ReadFrom.Configuration(configuration)`.

---

## 2. Оптимизация сборки (Docker Build)
- **Проблема:** контекст сборки весил **2.49 ГБ**.
- **Решение:** `.dockerignore` исключает `.git`, `bin/obj`, `node_modules`, медиа.
- **Результат:** контекст **~400 КБ**, сборка быстрее в ~50 раз.
- **NuGet `NU1301` при сборке .NET:** для этапа `build` добавлен `network: host` — сборка идёт через системный VPN/прокси хоста.

---

## 3. Симулятор (Python / pymodbus)
- **Проблема:** выход за границы адресов Modbus и несовместимость `pymodbus 3.x/4.x`.
- **Решение:** зафиксирована версия `pymodbus` в `sim/Dockerfile`; код на `ModbusSlaveContext`
  с доступом `context[slave_id]`.
- **Конфигурация БД:** убран hardcoded `127.0.0.1` в `devices`/соединении — `ip_address = 'sim'`
  (имя docker-сервиса), порт 5020.

Поведение тегов симулятора см. [Устройства и теги](Устройства%20и%20теги.md).

---

## 4. Cloudflare Tunnel (доступ снаружи)
- **Проблема:** разрывы туннеля каждые ~3 мин из-за таймаутов прокси (v2rayN).
- **Решение:**
  - **Protocol:** форсирован `http2` (TCP) — прокси не переваривают `quic` (UDP).
  - **Proxy:** HTTP-порт `10809`; туннель в `network_mode: host` (видит прокси на `127.0.0.1`).
  - **Heartbeat:** `--heartbeat-interval 5s` — держит канал активным.
  - **Bypass:** `NO_PROXY=localhost,127.0.0.1` — туннель не ищет WebApi в интернете.
- **API за туннелем:** в `Program.cs` отключён `app.UseHttpsRedirection()` (терминация SSL — на стороне
  Cloudflare, иначе цикл редиректов и статус «Down»). Swagger доступен всегда: `https://<domain>/swagger/`.

---

## 5. Контейнеризация веб-интерфейса (iiot_admin)
- **Архитектура сборки:** multi-stage Dockerfile (`oven/bun:1-alpine` для сборки бандла и `node:22-alpine` для минимального production-рантайма Nitro node-server).
- **Runtime-конфигурация:** Nuxt запущен в SPA-режиме (`routeRules: { '/**': { ssr: false } }`), обращается к API по адресу `NUXT_PUBLIC_API_BASE`.
- **CORS:** в `IIoT.WebApi` настроена поддержка динамического списка origins из переменной окружения `CORS_ALLOWED_ORIGINS` (или `Cors__AllowedOrigins`). Для локального запуска по умолчанию включены порты 3000 и 5173.

---

## 6. Шпаргалка команд

### Сборка и запуск
```bash
docker compose up -d --build      # пересборка и запуск в фоне (все сервисы, включая admin)
docker compose down -v --rmi local # полная очистка + удаление данных (ОСТОРОЖНО: -v стирает тома БД)
docker stop <name> && docker rm <name>  # ручное удаление контейнера
```

### Логи и диагностика
```bash
docker logs -f --tail 50 <name>   # следить за логами
docker ps -a                      # все контейнеры, включая упавшие
```

### Флаги
| Флаг | Полное имя | Описание |
|---|---|---|
| `-d` | `--detach` | фоновый режим |
| `-f` | `--follow` | держать поток логов открытым |
| `--build` | | принудительная пересборка образов |
| `-v` | `--volumes` | при `down` удаляет тома (БД сотрётся!) |
| `--rmi local` | `--remove-images` | удалить локальные образы без кастомного тега |
| `--tail N` | | последние N строк логов |
| `-it` | `-i` + `-t` | интерактивный TTY (для `docker exec -it ...`) |
| `--rm` | | авто-удаление контейнера после завершения |
