<script setup lang="ts">
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { computed } from 'vue'
import { useTheme } from './composables/useTheme'

const route = useRoute()
const { mode, cycleTheme } = useTheme()

// Only show main navigation (Header + Bottom Bar) on root tabs
const showNav = computed(() => ['index', 'logs', 'settings'].includes(route.name as string))

const modeIcon = computed(() => {
  if (mode.value === 'light') return 'fa-sun'
  if (mode.value === 'dark') return 'fa-moon'
  return 'fa-circle-half-stroke' // auto
})
</script>

<template>
  <!-- Presentation Stage -->
  <div
    class="relative flex h-full w-full items-center justify-center bg-zinc-950 p-4 font-sans text-ios-text antialiased h-screen w-screen overflow-hidden"
  >
    <!-- Phone Frame -->
    <div
      class="relative h-[852px] w-[393px] overflow-hidden rounded-[55px] border-[14px] border-[#1a1a1a] bg-ios-bg shadow-2xl ring ring-white/5 transition-colors duration-500 ease-apple"
    >
      <!-- Dynamic Island / Notch Area -->
      <div
        class="absolute top-0 left-0 right-0 z-[100] flex justify-center pt-2 pointer-events-none"
      >
        <div class="h-[36px] w-[120px] rounded-[24px] bg-black"></div>
      </div>

      <!-- Status Bar Time (Fake) -->
      <div
        class="absolute top-3.5 left-8 z-[100] text-[15px] font-semibold text-black dark:text-white mix-blend-difference pointer-events-none"
      >
        9:41
      </div>

      <!-- Status Bar Icons (Fake) -->
      <div
        class="absolute top-3.5 right-8 z-[100] flex gap-1.5 text-black dark:text-white mix-blend-difference pointer-events-none"
      >
        <i class="fa-solid fa-signal text-[12px]"></i>
        <i class="fa-solid fa-wifi text-[12px]"></i>
        <i class="fa-solid fa-battery-full text-[14px]"></i>
      </div>

      <!-- Inner App Scrollable Area -->
      <div class="h-full w-full overflow-y-auto overflow-x-hidden scrollbar-hide bg-ios-bg">
        <!-- Header -->
        <header
          v-if="showNav"
          class="sticky top-0 z-40 w-full backdrop-blur-xl bg-ios-nav-bg/80 pt-14 pb-4 px-6 transition-all duration-500"
        >
          <div class="flex items-center justify-between">
            <div class="flex flex-col">
              <h1 class="text-2xl font-bold tracking-tight leading-tight">Monitoring</h1>
              <span
                class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400"
                >Workshop A • Unit #1</span
              >
            </div>

            <!-- Theme Toggle Mini -->
            <button
              @click="cycleTheme"
              class="flex h-10 w-10 items-center justify-center rounded-full bg-black/5 dark:bg-white/10 active:scale-90 transition-transform"
            >
              <i class="fa-solid transition-transform duration-300 text-lg" :class="modeIcon"></i>
            </button>
          </div>
        </header>

        <!-- Main Content -->
        <main class="px-5 pb-32 pt-2 min-h-screen">
          <RouterView v-slot="{ Component }">
            <Transition name="page" mode="out-in">
              <component :is="Component" />
            </Transition>
          </RouterView>
        </main>
      </div>

      <!-- Mobile Bottom Navigation (Always Visible now) -->
      <div class="absolute bottom-8 left-0 right-0 z-50 flex justify-center">
        <nav
          v-if="showNav"
          class="flex items-center gap-10 rounded-[30px] border border-white/20 bg-ios-nav-bg/90 backdrop-blur-2xl px-8 py-5 shadow-2xl shadow-black/10"
        >
          <RouterLink
            to="/"
            class="group relative flex flex-col items-center justify-center gap-1 transition-all duration-300"
            active-class="text-blue-500"
            :class="route.path === '/' ? '' : 'text-gray-400 dark:text-gray-600'"
          >
            <i
              class="fa-solid fa-chart-line text-2xl transition-transform group-active:scale-75"
            ></i>
          </RouterLink>

          <RouterLink
            to="/logs"
            class="group relative flex flex-col items-center justify-center gap-1 transition-all duration-300"
            active-class="text-blue-500"
            :class="route.path === '/logs' ? '' : 'text-gray-400 dark:text-gray-600'"
          >
            <i
              class="fa-solid fa-list-check text-2xl transition-transform group-active:scale-75"
            ></i>
          </RouterLink>

          <RouterLink
            to="/settings"
            class="group relative flex flex-col items-center justify-center gap-1 transition-all duration-300"
            active-class="text-blue-500"
            :class="route.path === '/settings' ? '' : 'text-gray-400 dark:text-gray-600'"
          >
            <i class="fa-solid fa-gear text-2xl transition-transform group-active:scale-75"></i>
          </RouterLink>
        </nav>
      </div>

      <!-- Home Indicator -->
      <div
        class="absolute bottom-2 left-1/2 h-1.5 w-32 -translate-x-1/2 rounded-full bg-black/20 dark:bg-white/20"
      ></div>
    </div>
  </div>
</template>

<style>
/* Hide scrollbar */
.scrollbar-hide::-webkit-scrollbar {
  display: none;
}
.scrollbar-hide {
  -ms-overflow-style: none;
  scrollbar-width: none;
}
</style>
