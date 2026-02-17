### 1. Performance & Event Loop

**Проблема:** Блокировка основного потока тяжелыми вычислениями (парсинг больших данных, криптография).
**Legacy решения:**

- `setTimeout(..., 0)` — костыль.
- `scheduler.yield()` — лучше, но всё равно нагружает Main Thread.
  **Production-Ready решение:**
- **Web Workers.** Выноси тяжелую логику в отдельный поток.
- В стеке Vue: используй `useWebWorkerFn` из библиотеки VueUse.
- _Принцип:_ Не "нарезай" лаги, а устраняй их источник.

### 2. Frontend Optimization (Nuxt 4 Context)

**Anti-Patterns (запрещено):**

- ❌ **Icon Fonts (FontAwesome):** Блокируют рендеринг, FOUT, лишний вес.
  - _Замена:_ **SVG** через модуль `@nuxt/icon` (Iconify).
- ❌ **Manual Bundling:** Склеивание скриптов в один файл.
  - _Замена:_ Доверься **Vite** и HTTP/2 Multiplexing. Используй Lazy Loading компонентов.
- ❌ **Legacy Images:** JPG/PNG для тяжелой графики.
  - _Замена:_ **AVIF/WebP** через `@nuxt/image`.

### 3. Работа с датами (Time Management)

**Проблема:** Объект `Date` мутабелен, имеет кривой API (месяцы с 0) и плохой парсинг.
**Решение:**

- **Temporal API (Stage 3).** Новый стандарт. Неизменяемые объекты, строгая типизация (`PlainDate` vs `ZonedDateTime`).
