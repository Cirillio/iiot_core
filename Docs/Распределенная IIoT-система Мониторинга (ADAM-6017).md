# Архитектура Программно-Аппаратного Комплекса IIoT (v2.0)

**Стек:** .NET 8 / PostgreSQL (TimescaleDB) / Flutter / Vue 3 (Vite) / Docker

## 1. Концепция Системы

Система представляет собой распределенный комплекс для сбора, хранения и визуализации телеметрии с промышленных контроллеров (ADAM-6017).

**Ключевые принципы:**

1. **Raw Data First & Optimization:** Мы храним **и** сырые данные (для исторической правды), **и** откалиброванные значения (для быстрого доступа). Используем денормализацию ради производительности чтения.
    
2. **Smart Storage:** База данных берет на себя тяжелые задачи: сжатие данных, расчет средних значений (агрегатов) и очистку устаревших записей.
    
3. **Edge Independence:** Узел сбора данных автономен. Отказ сети не останавливает накопление данных благодаря локальному буферу.
    

## 2. Инфраструктура (Docker Host)

Развертывание через `docker-compose`. Доступ извне через Cloudflare Tunnel (Zero Trust).

### Сервисы (Containers):

1. **`db` (TimescaleDB HA):**
    
    - Образ: `timescale/timescaledb:latest-pg15` (вместо ванильного Postgres).
        
    - Функции: Гипертаблицы, непрерывная агрегация, фоновое сжатие (90%+ экономии места).
        
2. **`client` (Modbus Collector):**
    
    - .NET 8 Worker Service.
        
    - Опрос железа и первичная обработка данных.
        
3. **`gateway` (Web API):**
    
    - .NET 8 Minimal API.
        
    - Orchestrator: управляет потоками данных между БД и клиентами.
        
4. **`tunnel` (Cloudflared):**
    
    - Обеспечивает доступ по `https://api.domain.com` без белого IP.
        

## 3. Компоненты Системы (Deep Dive)

### 3.1. Modbus Client (Edge Node)

- **Логика записи:**
    
    - Получает код АЦП (например, `32768`).
        
    - Применяет настройки из кэша для расчета физической величины.
        
    - Пишет в БД **одну строку**: `INSERT INTO metrics (time, raw_value, value) ...`.
        
- **Типизация:** Поддерживает работу с `ANALOG` (float) и `DIGITAL` (0/1) сигналами, приводя их к единому формату `DOUBLE`.
    

### 3.2. Storage Layer (TimescaleDB)

"Умное" хранилище, реализующее Data Lifecycle Management.

**Схема данных:**

- `sensor_settings`: Метаданные, коэффициенты калибровки, формулы виртуальных датчиков и **JSON-конфиг UI** (цвет, иконка).
    
- `metrics` (Hypertable): Единая таблица для всех типов датчиков.
    
    - _Политика сжатия:_ Данные старше 7 дней сжимаются по алгоритму Gorilla/Delta-Delta.
        
- `metrics_hourly` (Materialized View): Автоматически обновляемые часовые агрегаты (AVG, MIN, MAX).
    
- `system_config`: Глобальные настройки сроков хранения (Retention Days).
    
- `maintenance_logs`: Журнал работы фоновых задач очистки.
    

**Автоматизация:**

- **Retention Job:** Хранимая процедура `prc_run_retention` запускается раз в сутки, читает настройки из `system_config` и удаляет старые чанки через `drop_chunks`.
    

### 3.3. Web API Gateway

- **Стек:** .NET 8, Dapper, SignalR.
    
- **Virtual Sensors Engine:** Вычисляет значения виртуальных каналов (например, `(P1+P2)/2`) на лету при поступлении данных.
    
- **SignalR Hub:** Транслирует поток `ReceiveMetrics` (живые данные) и `SystemAlert` (статусы сервисов).
    
- **Management API:** Эндпоинты для CRUD настроек датчиков и управления конфигурацией системы (`retention`).
    

## 4. Клиентские Приложения (UI)

### 4.1. Mobile App (Flutter)

Облегченный интерфейс для оперативного контроля. Подключается к тому же API, использует единые настройки отображения (`ui_config`) из БД.

### 4.2. Web Dashboard (Vue 3 SPA) — "Engineering Terminal"

Переход с Nuxt 4 на чистый Vue 3 для полного контроля над SPA-логикой.

- **Стек:** Vue 3.5 (Script Setup), Vite, Pinia (Store), Tailwind CSS.
    
- **Ключевые модули:**
    
    - **Dashboard:** Сетка виджетов с Drag&Drop. Отображение `value` из сокета.
        
    - **History:** Графики Apache ECharts. Загружают данные из `metrics_hourly` (мгновенный рендеринг за год).
        
    - **Calibration:** Формы редактирования `sensor_settings` с валидацией формул.
        
    - **System Health:** Панель управления сроками хранения данных. Отображение логов из `maintenance_logs`.
        

## 5. Потоки данных (Data Flow)

1. **Ingestion (Сбор):**
    
    ADAM $\rightarrow$ ModbusClient $\rightarrow$ (Raw + Scaled) $\rightarrow$ **DB (metrics)**.
    
2. **Streaming (Живой поток):**
    
    DB (Insert Trigger) $\rightarrow$ API $\rightarrow$ SignalR $\rightarrow$ **Vue Client (Dashboard)**.
    
3. **Analytics (История):**
    
    Vue Client $\rightarrow$ API $\rightarrow$ `SELECT FROM metrics_hourly` (Сжатые агрегаты) $\rightarrow$ **ECharts**.
    
4. **Configuration (Управление):**
    
    Vue Client $\rightarrow$ API $\rightarrow$ `UPDATE sensor_settings` $\rightarrow$ DB Trigger (`updated_at`).
    

## 6. Обоснование решений (Для защиты)

- **Почему TimescaleDB Image?**
    
    Стандартный Postgres не умеет сжимать временные ряды. Использование специализированного образа позволило сократить объем диска в 10 раз и ускорить аналитические запросы.
    
- **Почему Unified Table (одна таблица metrics)?**
    
    Упрощает построение сводных графиков (наложение давления и температуры на одну ось) и позволяет использовать единый механизм сжатия для всех типов данных.
    
- **Почему настройки UI в БД?**
    
    Принцип "Single Source of Truth". И инженер в веб-панели, и оператор с мобильным приложением видят одинаковые цвета и иконки датчиков, что исключает путаницу при авариях.