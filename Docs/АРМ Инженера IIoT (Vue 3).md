# АРМ Инженера IIoT (Vue 3 + Vite)

## 1. Обзор и Цели

Веб-приложение (далее "Dashboard") служит единой точкой входа для управления комплексом мониторинга. Оно не хранит данные, а визуализирует их, получая от WebAPI Gateway.

**Изменения в архитектуре v2.0:**
Переход с Nuxt 4 на чистый **Vue 3 (Vite)** обусловлен отсутствием необходимости в SSR и желанием упростить деплой (статика Nginx) и получить полный контроль над SPA-логикой.

**Ключевые принципы:**
1. **SPA Mode:** Мгновенная реактивность клиента. Статическая сборка (`npm run build`).
2. **Type Safety:** Строгая типизация всех входящих DTO от C# API. Zod для валидации форм.
3. **Real-time First:** Интерфейс обновляется без перезагрузки страницы (SignalR).

## 2. Технологический Стек (Утверждено v2.0)

- **Core:** `Vue 3.5` (Script Setup) + `Vite`.
- **Styling:** `Tailwind CSS` (utility-first).
- **State Management:** `Pinia` (Stores: Auth, Sensors, Socket, System).
- **Validation:** `Zod` (валидация JSON-схем калибровки и форм).
- **Charts:** `Apache ECharts` (через `vue-echarts`). Оптимизирован для рендеринга 10k+ точек.
- **Router:** `Vue Router` (History mode).
- **Icons:** `Unplugin Icons` (Material Design).
- **Utils:** `VueUse` (LocalStorage, ResizeObserver).

## 3. Структура Проекта (Vue 3 Standard)

```
src/
├── assets/           # Статика (лого, стили)
├── components/
│   ├── charts/       # Обертки над ECharts
│   ├── dashboard/    # Виджеты (SensorWidget, ValueCard)
│   ├── calibration/  # Формы настроек
│   └── ui/           # Базовые UI компоненты (Button, Input)
├── composables/
│   ├── useSignalR.ts # Логика соединения с Hub
│   └── useApi.ts     # Обертка над fetch с типизацией
├── layouts/
│   └── MainLayout.vue # Sidebar + Header
├── views/
│   ├── Dashboard.vue # Живой мониторинг (Grid)
│   ├── Analytics.vue # Исторические графики
│   ├── Settings.vue  # Калибровка датчиков
│   └── System.vue    # Статус сервисов и логи
├── stores/
│   ├── socketStore.ts # Состояние соединения
│   └── sensorStore.ts # Map<Id, SensorData>
├── types/
│   └── dto.d.ts      # TypeScript интерфейсы (Metrics, Settings)
├── utils/
│   └── formatters.ts # Форматирование чисел и дат
├── App.vue
└── main.ts
```

## 4. Модульная Архитектура

### 4.1. Core Module: SignalR & Real-time Data
Центральный нерв приложения.

- **Реализация (`useSignalR` + `socketStore`):**
    - Единое подключение на всё приложение.
    - **Auto-reconnect:** Автоматическое переподключение при обрыве связи.
    - **События:**
        - `ReceiveMetrics`: Обновляет реактивный `Map` в Pinia.
        - `SystemAlert`: Показывает Toast уведомление (Global Notification).

### 4.2. Модуль "Живой Мониторинг" (Dashboard View)
Пользователь настраивает сетку под себя.

- **Функционал:**
    - **Grid Layout:** Адаптивная сетка карточек датчиков.
    - **Drag & Drop:** Возможность менять порядок карточек (сохраняется в `localStorage`).
- **Визуализация:**
    - Данные о цвете и иконке берутся из `sensor_settings.ui_config` (приходит с бэкенда).
    - Индикация устаревания данных (серой вуалью, если нет обновлений > 1 мин).

### 4.3. Модуль "Инженерная Калибровка" (Settings View)
Управление метаданными датчиков (`sensor_settings`).

- **UI:** Список датчиков с поиском. Редактирование в модальном окне или Slide-over.
- **Поля (согласно DB):**
    - `input_min` / `input_max` (АЦП).
    - `output_min` / `output_max` (Физическая величина).
    - `formula` (Для виртуальных датчиков).
- **Валидация (Zod):**
    ```typescript
    const calibrationSchema = z.object({
      input_min: z.number(),
      input_max: z.number(),
      output_min: z.number(),
      output_max: z.number(),
    }).refine(data => data.input_max > data.input_min, {
      message: "Max ADC must be greater than Min ADC"
    });
    ```

### 4.4. Модуль "Аналитика" (Analytics View)
Работа с историческими данными (Hypertable).

- **Источник данных:** Представление `metrics_hourly` (TimescaleDB Continuous Aggregate).
- **Функции:**
    - Выбор диапазона дат (DateRangePicker).
    - Мульти-осевые графики (температура и давление на одном поле).
    - Экспорт PNG/CSV.

### 4.5. Модуль "System Health"
Мониторинг инфраструктуры.

- **Данные из таблицы `system_status`:**
    - Статус сервисов (`ONLINE`, `OFFLINE`).
    - `uptime_seconds`, `last_error`.
- **Управление Retention (`system_config`):**
    - Форма изменения сроков хранения (`raw_retention_days`, `agg_retention_days`).
    - Отображение логов очистки из `maintenance_logs`.

## 5. Потоки данных (Data Flow)

### Сценарий 1: Живой поток (1 Гц)
1. **DB Trigger:** Новая запись в `metrics`.
2. **API:** Отправляет `MetricsDTO` через SignalR.
3. **Vue Store:** Обновляет значение в `sensorStore`.
4. **UI:** Компонент `ValueCard` реактивно обновляет цифру (без перерисовки всего компонента).

### Сценарий 2: Сохранение настроек
1. **UI:** Инженер меняет калибровку -> `useApi.post('/api/sensors/{id}', data)`.
2. **API:** Валидирует, пишет в `sensor_settings`.
3. **API:** Рассылает событие `ConfigUpdated` всем подключенным клиентам.
4. **Vue Store:** Обновляет локальный кэш настроек (границы, юниты), пересчитывает отображение.

## 6. UX и Безопасность

- **Обработка ошибок:**
    - Глобальный перехватчик ошибок Axios/Fetch.
    - Всплывающие уведомления (Toast) при ошибках сети или валидации.
- **Индикация связи:**
    - Иконка "Network Status" в хедере (Зеленый/Красный).
    - Блокировка критических действий при потере соединения.