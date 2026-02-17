<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router'
import SensorCard from '../components/SensorCard.vue'

const router = useRouter()
const route = useRoute()
const assetId = route.params.slug as string

// Mock filtered data
const assetName = "Boiler Feed Pump B"
const sensors = [
  { id: 'CH-1', title: 'Pressure', value: 8.2, unit: 'Bar', icon: 'fa-solid fa-wind', iconColor: 'text-blue-500', barColor: 'bg-red-500', barWidth: '98%', isCritical: true },
  { id: 'CH-3', title: 'Voltage', value: 24.1, unit: 'V', icon: 'fa-solid fa-bolt', iconColor: 'text-yellow-500', barColor: 'bg-yellow-400', barWidth: '95%' },
]
</script>

<template>
  <div class="space-y-6 pb-24 relative">
    <!-- Filter Header -->
    <div class="bg-blue-500/10 backdrop-blur-md p-6 rounded-[2rem] flex items-center justify-between border border-blue-500/10">
      <div>
        <span class="text-[11px] uppercase font-bold text-blue-600 dark:text-blue-400 tracking-widest">Filtered View</span>
        <h2 class="text-ios-text font-bold text-xl leading-tight mt-1">{{ assetName }}</h2>
        <span class="text-xs text-gray-500 font-mono">{{ assetId }}</span>
      </div>
      <button @click="router.push('/')" class="w-10 h-10 flex items-center justify-center rounded-full bg-blue-500/20 text-blue-600 dark:text-blue-400 hover:bg-blue-500/30 transition-all active:scale-95">
        <i class="fa-solid fa-xmark"></i>
      </button>
    </div>

    <!-- Sensor List -->
    <div class="grid grid-cols-1 gap-4">
      <SensorCard 
        v-for="sensor in sensors" 
        :key="sensor.id"
        v-bind="sensor"
        @click="router.push(`/sensor/${sensor.id}`)"
      />
    </div>

    <!-- Empty State / Placeholder -->
    <div class="text-center py-10 opacity-60">
      <div class="w-16 h-16 bg-gray-100 dark:bg-white/5 rounded-full flex items-center justify-center mx-auto mb-4">
        <i class="fa-solid fa-link text-2xl text-gray-400"></i>
      </div>
      <p class="text-gray-500 text-sm font-medium">2 Linked Sensors Found</p>
    </div>
  </div>
</template>