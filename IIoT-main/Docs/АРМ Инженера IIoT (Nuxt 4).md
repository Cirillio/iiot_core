# АРМ Инженера IIoT (Nuxt 4 + Nuxt UI) - v3.0

## 1. Обзор и Цели

Веб-приложение (далее "Dashboard") служит единой точкой входа для управления комплексом мониторинга.

**Архитектура v3.0:**
Переход на **Nuxt 4** для улучшения Developer Experience (DX), типизации и модульности. Приложение может работать в режиме SPA (`ssr: false`) для встраивания в .NET API Gateway (Embedded Frontend) или как полноценный SSR сервис.

**Ключевые принципы:**
1.  **Nuxt Architecture:** Использование File-based routing, Auto-imports и модульной системы Nuxt.
2.  **Nuxt UI:** Профессиональная дизайн-система на базе Tailwind CSS и Headless UI.
3.  **Type Safety:** Строгая типизация от бэкенда до фронтенда (Zod DTOs).
4.  **Real-time First:** Глубокая интеграция SignalR для мгновенного обновления данных.

## 2. Технологический Стек

- **Framework:** `Nuxt 4` (Latest Stable/Beta).
- **UI Library:** `Nuxt UI` (включает Tailwind CSS v4, Color Mode, Icons).
- **State Management:** `Pinia` (Stores: Auth, Sensors, Socket, System).
- **Validation:** `Zod` (схемы валидации форм и API ответов).
- **Charts:** `vue-echarts` (Apache ECharts) для производительных графиков.
- **Icons:** `Nuxt Icon` (доступ к 100k+ иконок через Iconify).
- **Networking:** `ofetch` (встроенный в Nuxt) + SignalR Client.

## 3. Структура Проекта (Nuxt 4)

```text
client/
├── app.config.ts         # Конфигурация темы Nuxt UI
├── app.vue               # Корневой компонент
├── nuxt.config.ts        # Основной конфиг (модули, SSR, прокси)
├── components/
│   ├── charts/           # Обертки ECharts (TrendChart, Gauge)
│   ├── dashboard/        # Виджеты (SensorCard, AlertBanner)
│   └── settings/         # Формы калибровки (CalibrationForm)
├── composables/
│   ├── useSignalR.ts     # Управление WebSocket соединением
│   └── useSystemApi.ts   # Обертки над REST API
├── layouts/
│   └── default.vue       # Sidebar + Topbar layout
├── pages/
│   ├── index.vue         # Дашборд (Live Monitoring)
│   ├── analytics.vue     # Графики и история
│   ├── devices.vue       # Управление контроллерами (ADAM-6017)
│   ├── settings.vue      # Настройки датчиков и системы
│   └── system.vue        # Системные логи и статус сервисов
├── public/               # Статические ассеты
├── stores/
│   ├── socket.ts         # Состояние соединения и буфер данных
│   └── sensors.ts        # Хранилище метрик (Map<Id, Data>)
└── types/
    └── api.d.ts          # TypeScript интерфейсы (DTO)
```

## 4. Модульная Архитектура и UI Компоненты

### 4.1. Core: SignalR Integration
Реализовано через Composable `useSignalR`.
- **Global Provider:** Инициализируется в `app.vue` или плагине.
- **Events:** `ReceiveMetrics`, `ConfigUpdated`, `SystemAlert`.
- **Reactivity:** Обновляет Pinia Store, который реактивно меняет UI.

### 4.2. Дашборд (`index.vue`)
Использует Grid-систему Tailwind.
- **Sensor Cards:** Используют компонент `UCard` из Nuxt UI.
- **Индикация:** Цветовое кодирование (Green/Yellow/Red) через динамические классы Tailwind в зависимости от порогов.
- **Skeleton Loading:** `USkeleton` при загрузке данных.

### 4.3. Управление Устройствами (`devices.vue`)
- **Table:** Компонент `UTable` для отображения списка контроллеров.
- **Actions:** Dropdown меню (`UDropdown`) для операций (Ping, Edit, Delete).
- **Forms:** Модальные окна (`UModal`) с формами на базе `UForm` + Zod валидация.

### 4.4. Настройки и Калибровка (`settings.vue`)
Интерфейс инженера для изменения метаданных.
- **Search:** `UInput` с иконкой поиска для фильтрации датчиков.
- **Edit Mode:** Slide-over панель (`USlideover`) для редактирования формул и границ.
- **Notifications:** `UTast` для уведомлений об успешном сохранении.

## 5. Потоки данных (Data Flow)

1.  **SSR/Universal Load:** При первой загрузке Nuxt запрашивает конфигурацию системы (`/api/config`) через `useAsyncData` на сервере (или клиенте в SPA режиме).
2.  **Real-time Updates:** SignalR "пушит" обновления. Стор мутирует состояние. Графики перерисовываются.
3.  **User Actions:** Пользователь меняет настройки -> POST запрос к API -> API оповещает всех клиентов через SignalR -> Интерфейсы обновляются.