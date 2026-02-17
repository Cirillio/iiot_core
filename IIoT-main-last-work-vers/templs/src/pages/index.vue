<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import SensorCard from '../components/SensorCard.vue'

const router = useRouter()

const showAckModal = ref(false)
const alertAcknowledged = ref(false)

const sensors = [
  { id: 'CH-0', title: 'Temperature', value: 62.4, unit: '°C', icon: 'fa-solid fa-temperature-half', iconColor: 'text-orange-500', barColor: 'bg-gradient-to-r from-orange-400 to-orange-600', barWidth: '60%' },
  { id: 'CH-1', title: 'Pressure', value: 8.2, unit: 'Bar', icon: 'fa-solid fa-wind', iconColor: 'text-blue-500', barColor: 'bg-red-500', barWidth: '98%', isCritical: true },
  { id: 'CH-2', title: 'Humidity', value: 45.1, unit: '%', icon: 'fa-solid fa-droplet', iconColor: 'text-blue-500', barColor: 'bg-blue-500', barWidth: '45%' },
  { id: 'CH-3', title: 'Voltage', value: 24.1, unit: 'V', icon: 'fa-solid fa-bolt', iconColor: 'text-yellow-500', barColor: 'bg-yellow-400', barWidth: '95%' },
]

const handleAck = () => {
  alertAcknowledged.value = true
  showAckModal.value = false
}
</script>

<template>
  <div class="space-y-8">
    <!-- Header Title -->
    <div class="px-1 flex justify-between items-end">
      <div>
        <h2 class="text-3xl font-bold tracking-tight text-ios-text">Overview</h2>
        <p class="text-sm text-gray-500 font-medium mt-1">Real-time monitoring</p>
      </div>
    </div>

    <!-- Critical Alert Banner (Soft) -->
    <div 
      v-if="!alertAcknowledged"
      class="bg-red-500/10 backdrop-blur-md p-5 rounded-3xl flex items-center gap-5 cursor-pointer hover:bg-red-500/15 transition-all duration-300 group" 
      @click="showAckModal = true"
    >
      <div class="flex-shrink-0 w-12 h-12 rounded-full bg-red-500 flex items-center justify-center shadow-lg shadow-red-500/30 group-active:scale-95 transition-transform">
        <i class="fa-solid fa-bell text-white text-lg"></i>
      </div>
      <div class="flex-1">
        <h3 class="text-red-600 dark:text-red-400 font-bold text-base">Critical Alert</h3>
        <p class="text-red-600/80 dark:text-red-400/80 text-sm mt-0.5 font-medium">Pressure P-102 exceeded safe limit (8.2 Bar).</p>
      </div>
      <div class="w-8 h-8 rounded-full bg-red-500/10 flex items-center justify-center group-hover:translate-x-1 transition-transform">
        <i class="fa-solid fa-chevron-right text-red-500 text-xs"></i>
      </div>
    </div>

    <!-- Actions -->
    <div class="grid grid-cols-2 gap-4">
      <router-link to="/qr-scan" class="group relative overflow-hidden bg-blue-600 hover:bg-blue-500 text-white p-6 rounded-[2rem] flex flex-col items-center justify-center gap-3 shadow-xl shadow-blue-500/20 transition-all active:scale-[0.98]">
        <div class="w-12 h-12 rounded-full bg-white/20 flex items-center justify-center mb-1">
             <i class="fa-solid fa-qrcode text-2xl"></i>
        </div>
        <span class="font-semibold text-sm tracking-wide">Scan Sensor</span>
      </router-link>
      <button class="bg-ios-card hover:bg-gray-50 dark:hover:bg-white/5 text-ios-text p-6 rounded-[2rem] flex flex-col items-center justify-center gap-3 shadow-sm hover:shadow-md transition-all active:scale-[0.98]">
        <div class="w-12 h-12 rounded-full bg-gray-100 dark:bg-white/10 flex items-center justify-center mb-1 text-gray-500 dark:text-gray-400">
             <i class="fa-solid fa-sliders text-xl"></i>
        </div>
        <span class="font-semibold text-sm tracking-wide">Filter View</span>
      </button>
    </div>

    <!-- Virtual Metrics (Glassy Dark Card) -->
    <section>
      <div class="flex justify-between items-baseline mb-4 px-2">
        <h2 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Efficiency</h2>
      </div>
      <div class="relative overflow-hidden bg-black dark:bg-zinc-900 text-white p-7 rounded-[2rem] shadow-2xl shadow-black/10 group cursor-default">
        <!-- Decorational blur -->
        <div class="absolute top-0 right-0 -mt-10 -mr-10 w-40 h-40 bg-blue-500/40 rounded-full blur-3xl opacity-60 group-hover:opacity-80 transition-opacity duration-700"></div>

        <div class="relative z-10 flex justify-between items-start">
          <div>
            <p class="text-zinc-400 text-sm font-medium mb-1">Overall Ratio</p>
            <h3 class="text-5xl font-bold tracking-tight">94.5 <span class="text-2xl text-zinc-500 font-medium">%</span></h3>
          </div>
          <div class="bg-white/10 backdrop-blur-md w-12 h-12 rounded-full flex items-center justify-center border border-white/10">
            <i class="fa-solid fa-gauge-high text-blue-400 text-lg"></i>
          </div>
        </div>
        <div class="mt-8 h-2 w-full bg-white/10 rounded-full overflow-hidden">
          <div class="h-full bg-blue-500 w-[94.5%] shadow-[0_0_15px_rgba(59,130,246,0.6)] rounded-full"></div>
        </div>
        <div class="flex justify-between items-center mt-4">
             <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-green-500/20 border border-green-500/20 text-green-400 text-[10px] font-bold tracking-wide uppercase">
                <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse"></span> Optimal
             </span>
             <p class="text-[10px] text-zinc-500 font-mono opacity-70">UPDATED 10:42:05</p>
        </div>
      </div>
    </section>

    <!-- Live Telemetry -->
    <section>
      <div class="flex justify-between items-baseline mb-4 px-2">
        <h2 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Sensors</h2>
      </div>

      <div class="flex flex-col gap-6">
        <SensorCard 
          v-for="sensor in sensors" 
          :key="sensor.id"
          v-bind="sensor"
          @click="router.push(`/sensor/${sensor.id}`)"
        />
      </div>
    </section>

    <!-- System Health Summary -->
    <section>
      <div class="flex justify-between items-baseline mb-4 px-2">
        <h2 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Health</h2>
        <router-link to="/system-health" class="text-xs font-semibold text-blue-600 dark:text-blue-400 hover:opacity-70 transition-opacity">View All</router-link>
      </div>
      <div class="bg-ios-card rounded-[2rem] p-2 shadow-sm">
        <div class="divide-y divide-gray-100 dark:divide-white/5">
            <div class="flex justify-between items-center p-4">
            <div class="flex items-center gap-4">
                <div class="w-10 h-10 rounded-2xl bg-green-500/10 flex items-center justify-center text-green-600 dark:text-green-400">
                    <i class="fa-solid fa-server text-sm"></i>
                </div>
                <span class="text-sm font-semibold text-ios-text">Modbus Collector</span>
            </div>
            <div class="w-2.5 h-2.5 rounded-full bg-green-500 shadow-sm shadow-green-500/50"></div>
            </div>

            <div class="flex justify-between items-center p-4">
            <div class="flex items-center gap-4">
                <div class="w-10 h-10 rounded-2xl bg-green-500/10 flex items-center justify-center text-green-600 dark:text-green-400">
                    <i class="fa-solid fa-database text-sm"></i>
                </div>
                <span class="text-sm font-semibold text-ios-text">TimescaleDB</span>
            </div>
             <div class="w-2.5 h-2.5 rounded-full bg-green-500 shadow-sm shadow-green-500/50"></div>
            </div>

            <div class="flex justify-between items-center p-4">
            <div class="flex items-center gap-4">
                <div class="w-10 h-10 rounded-2xl bg-yellow-500/10 flex items-center justify-center text-yellow-600 dark:text-yellow-400">
                    <i class="fa-solid fa-cloud text-sm"></i>
                </div>
                <span class="text-sm font-semibold text-ios-text">Cloudflare Tunnel</span>
            </div>
            <div class="w-2.5 h-2.5 rounded-full bg-yellow-500 shadow-sm shadow-yellow-500/50 animate-pulse"></div>
            </div>
        </div>
      </div>
    </section>

    <!-- Acknowledgement Modal -->
    <div v-if="showAckModal" class="fixed inset-0 z-[100] flex items-end sm:items-center justify-center bg-black/20 dark:bg-black/60 backdrop-blur-sm p-4 transition-all duration-300">
      <div class="bg-ios-card w-full max-w-sm rounded-[2.5rem] p-8 space-y-8 shadow-2xl ring-1 ring-black/5 dark:ring-white/10 transform transition-all scale-100 animate-slide-up">
        <div class="text-center">
          <div class="w-20 h-20 bg-red-100 dark:bg-red-500/20 rounded-full flex items-center justify-center mx-auto mb-6">
            <i class="fa-solid fa-bell text-red-600 dark:text-red-400 text-3xl"></i>
          </div>
          <h3 class="text-ios-text font-bold text-2xl">Acknowledge Alarm?</h3>
          <p class="text-gray-500 dark:text-gray-400 text-base mt-2 font-medium">Pressure P-102 (8.2 Bar)</p>
        </div>
        <div class="space-y-3">
          <button @click="handleAck" class="w-full bg-yellow-500 hover:bg-yellow-400 active:bg-yellow-600 text-white font-bold py-4 rounded-2xl transition-all shadow-lg shadow-yellow-500/20 active:scale-95">
            Acknowledge
          </button>
          <button @click="showAckModal = false" class="w-full bg-gray-100 dark:bg-zinc-800 hover:bg-gray-200 dark:hover:bg-zinc-700 text-ios-text font-bold py-4 rounded-2xl transition-all active:scale-95">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>