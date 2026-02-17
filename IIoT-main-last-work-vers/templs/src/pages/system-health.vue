<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <div class="space-y-6 pb-24">
    <!-- Header -->
    <div class="flex items-center gap-4 px-1">
      <button @click="router.back()" class="w-10 h-10 rounded-full bg-ios-card shadow-sm flex items-center justify-center text-gray-500 hover:text-gray-900 dark:hover:text-white transition-colors active:scale-95">
        <i class="fa-solid fa-arrow-left"></i>
      </button>
      <div>
        <h2 class="text-2xl font-bold text-ios-text">System Health</h2>
        <p class="text-sm text-gray-500 font-medium">Infrastructure Status</p>
      </div>
    </div>

    <!-- Overall Status -->
    <div class="bg-green-500/10 backdrop-blur-md rounded-[2rem] p-8 text-center border border-green-500/10">
      <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-green-500 shadow-lg shadow-green-500/30 mb-4 animate-pulse">
        <i class="fa-solid fa-check text-white text-2xl"></i>
      </div>
      <h3 class="text-green-600 dark:text-green-400 font-bold text-xl">All Systems Operational</h3>
      <p class="text-green-600/70 dark:text-green-400/70 text-sm mt-2 font-medium">Last updated: Just now</p>
    </div>

    <!-- Services Grid -->
    <div class="space-y-3">
      <!-- Modbus Collector -->
      <div class="bg-ios-card rounded-3xl p-5 flex justify-between items-center shadow-sm hover:shadow-md transition-all duration-300">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-2xl bg-gray-100 dark:bg-white/5 flex items-center justify-center text-gray-400">
            <i class="fa-solid fa-server text-lg"></i>
          </div>
          <div>
            <p class="text-[15px] text-ios-text font-semibold">Modbus Collector</p>
            <p class="text-xs text-gray-500 font-mono mt-0.5">Uptime: 4d 12h</p>
          </div>
        </div>
        <span class="text-[11px] font-bold text-green-600 bg-green-500/10 px-3 py-1.5 rounded-full">ONLINE</span>
      </div>

      <!-- Database -->
      <div class="bg-ios-card rounded-3xl p-5 flex justify-between items-center shadow-sm hover:shadow-md transition-all duration-300">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-2xl bg-gray-100 dark:bg-white/5 flex items-center justify-center text-gray-400">
            <i class="fa-solid fa-database text-lg"></i>
          </div>
          <div>
            <p class="text-[15px] text-ios-text font-semibold">TimescaleDB</p>
            <p class="text-xs text-gray-500 font-mono mt-0.5">Storage: 45% used</p>
          </div>
        </div>
        <span class="text-[11px] font-bold text-green-600 bg-green-500/10 px-3 py-1.5 rounded-full">ONLINE</span>
      </div>

      <!-- Cloudflare -->
      <div class="bg-ios-card rounded-3xl p-5 flex justify-between items-center shadow-sm hover:shadow-md transition-all duration-300">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-2xl bg-gray-100 dark:bg-white/5 flex items-center justify-center text-gray-400">
            <i class="fa-solid fa-cloud text-lg"></i>
          </div>
          <div>
            <p class="text-[15px] text-ios-text font-semibold">Cloudflare Tunnel</p>
            <p class="text-xs text-orange-500 font-mono mt-0.5">Latency: 450ms</p>
          </div>
        </div>
        <span class="text-[11px] font-bold text-orange-500 bg-orange-500/10 px-3 py-1.5 rounded-full">DEGRADED</span>
      </div>
    </div>

    <!-- Logs Preview -->
    <div class="bg-black/5 dark:bg-black/40 rounded-3xl p-6 font-mono text-[11px] leading-relaxed text-gray-600 dark:text-gray-400 h-48 overflow-y-auto inner-shadow">
      <p><span class="text-blue-500 font-bold">10:42:01</span> [INFO] Health check pulse sent</p>
      <p><span class="text-blue-500 font-bold">10:42:02</span> [INFO] DB Write Latency: 12ms</p>
      <p><span class="text-orange-500 font-bold">10:42:05</span> [WARN] Tunnel latency spike detected</p>
      <p><span class="text-blue-500 font-bold">10:42:10</span> [INFO] Modbus polling cycle complete</p>
    </div>
  </div>
</template>