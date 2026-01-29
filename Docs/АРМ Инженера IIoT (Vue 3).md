# АРМ Инженера IIoT (Vue 3 + Vite) - v2.1

## 1. Обзор и Цели

Веб-приложение (далее "Dashboard") служит единой точкой входа для управления комплексом мониторинга. Оно не хранит данные, а визуализирует их, получая от WebAPI Gateway.

**Изменения в архитектуре v2.1:**
Приложение теперь раздается через **Embedded Frontend** (встроено в .NET API Gateway), что упрощает деплой и устраняет необходимость в отдельном Nginx контейнере для статики.

**Ключевые принципы:**
1. **SPA Mode:** Мгновенная реактивность клиента. Статическая сборка, обслуживаемая бэкендом.
2. **Type Safety:** Строгая типизация всех входящих DTO от C# API. Zod для валидации форм.
3. **Real-time First:** Интерфейс обновляется без перезагрузки страницы (SignalR).
4. **Admin Focus:** Полный доступ к настройкам системы и управлению устройствами.

## 2. Технологический Стек

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
    - **SessionGuard:** Контроль лимита сессий (если API вернет ошибку переполнения).

### 4.2. Модуль "Живой Мониторинг" (Dashboard View)
Пользователь настраивает сетку под себя.

- **Функционал:**
    - **Grid Layout:** Адаптивная сетка карточек датчиков.
    - **Drag & Drop:** Возможность менять порядок карточек (сохраняется в `localStorage`).
- **Визуализация:**
    - Данные о цвете и иконке берутся из `sensor_settings.ui_config` (приходит с бэкенда).

### 4.3. Модуль "Device Management" (Новое в v2.1)
Управление источниками данных.

- **Функционал:**
    - CRUD интерфейс для таблицы `devices`.
    - Добавление/Удаление контроллеров ADAM-6017 (IP, Port, SlaveID) без перезагрузки коллектора.
    - Проверка статуса подключения к устройству.

### 4.4. Модуль "Инженерная Калибровка" (Settings View)
Управление метаданными датчиков (`sensor_settings`).

- **UI:** Список датчиков с поиском. Редактирование в модальном окне.
- **Поля:** `input_min`/`max`, `output_min`/`max`, `formula` (виртуальные).
- **Валидация (Zod):** Проверка логики диапазонов.

### 4.5. Модуль "Аналитика" и "System Health"
- **Аналитика:** Графики по данным из `metrics_hourly` (TimescaleDB).
- **System Health:** Статус сервисов, логи, управление Retention Policy (`system_config`).

## 5. Потоки данных (Data Flow)

### Сценарий 1: Живой поток
1. **DB Trigger:** Новая запись в `metrics`.
2. **API:** Отправляет `MetricsDTO` через SignalR.
3. **Vue Client:** Обновляет стор и UI.

### Сценарий 2: Управление Настройками
1. **Инженер (Vue):** Изменяет калибровку или добавляет устройство -> API.
2. **API:** Обновляет БД.
3. **API:** Рассылает событие `ConfigUpdated`.
4. **Коллектор/Мобайл:** Получают уведомление и обновляют свои кэши.
