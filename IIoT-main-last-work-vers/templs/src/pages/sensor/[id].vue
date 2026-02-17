<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const sensorId = route.params.id as string

// Mock data based on ID
const sensorData = {
  id: sensorId,
  name: 'Pressure P-102',
  value: 8.2,
  unit: 'Bar',
  min: 4.1,
  max: 8.5,
  limitHigh: 8.0,
  limitLow: 2.0,
  history: [4.2, 4.5, 4.8, 5.1, 5.5, 6.0, 6.2, 6.8, 7.5, 8.0, 8.2, 8.1, 8.3, 8.2]
}

// Simple SVG Sparkline
const width = 300
const height = 100
const maxVal = Math.max(...sensorData.history, sensorData.limitHigh + 1)
const minVal = Math.min(...sensorData.history, sensorData.limitLow - 1)
const range = maxVal - minVal

const points = sensorData.history.map((val, i) => {
  const x = (i / (sensorData.history.length - 1)) * width
  const y = height - ((val - minVal) / range) * height
  return `${x},${y}`
}).join(' ')

const thresholdY = height - ((sensorData.limitHigh - minVal) / range) * height
</script>

<template>
  <div class="pb-24">
    <!-- Header -->
    <div class="sticky top-0 bg-ios-nav-bg/80 backdrop-blur-xl p-4 flex items-center gap-4 z-10 -mx-6 px-6 border-b border-black/5 dark:border-white/5">
      <button @click="router.back()" class="w-10 h-10 rounded-full bg-ios-card shadow-sm flex items-center justify-center text-gray-500 hover:text-gray-900 dark:hover:text-white transition-all active:scale-95 border border-black/5 dark:border-white/5">
        <i class="fa-solid fa-arrow-left"></i>
      </button>
      <div>
        <h1 class="text-ios-text font-bold text-xl leading-tight">{{ sensorData.name }}</h1>
        <span class="text-xs text-gray-500 font-mono tracking-wide">{{ sensorData.id }} • Analog Input</span>
      </div>
    </div>

    <div class="space-y-8 mt-6">
      <!-- Main Value -->
      <div class="text-center py-4">
        <div class="inline-flex items-baseline gap-2">
          <span class="text-7xl font-bold tracking-tighter text-ios-text">{{ sensorData.value }}</span>
          <span class="text-2xl text-gray-400 font-medium">{{ sensorData.unit }}</span>
        </div>
        <div class="mt-4 inline-flex items-center gap-2 px-4 py-1.5 bg-red-500/10 rounded-full">
          <div class="w-2 h-2 bg-red-500 rounded-full animate-pulse shadow-[0_0_8px_rgba(239,68,68,0.6)]"></div>
          <span class="text-xs text-red-600 dark:text-red-400 font-bold uppercase tracking-wide">Critical High</span>
        </div>
      </div>

      <!-- Trend Chart -->
      <div class="bg-ios-card rounded-[2rem] p-6 relative overflow-hidden shadow-sm">
        <div class="flex justify-between items-center mb-6">
          <h3 class="text-sm text-gray-400 font-bold uppercase tracking-widest">1 Hour Trend</h3>
          <span class="text-xs text-green-600 bg-green-500/10 px-2.5 py-1 rounded-full font-bold">+12%</span>
        </div>
        
        <div class="h-40 w-full relative">
          <!-- Threshold Line -->
          <div class="absolute w-full border-t border-dashed border-red-500/30 z-0 flex items-center" :style="{ top: `${thresholdY}px` }">
            <span class="text-[10px] text-red-500/70 font-mono bg-ios-card px-1 absolute right-0 -top-2">Limit: {{ sensorData.limitHigh }}</span>
          </div>

          <svg :viewBox="`0 0 ${width} ${height}`" class="w-full h-full overflow-visible">
            <!-- Gradient Def -->
            <defs>
              <linearGradient id="gradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#ef4444" stop-opacity="0.4"/>
                <stop offset="100%" stop-color="#ef4444" stop-opacity="0"/>
              </linearGradient>
            </defs>
            
            <!-- Area -->
            <path 
              :d="`M0,${height} ${points} L${width},${height} Z`" 
              fill="url(#gradient)" 
            />
            
            <!-- Line -->
            <polyline 
              :points="points" 
              fill="none" 
              stroke="#ef4444" 
              stroke-width="4" 
              stroke-linecap="round" 
              stroke-linejoin="round"
            />
            
            <!-- Last Point Dot -->
            <circle :cx="width" :cy="height - ((sensorData.value - minVal) / range) * height" r="6" class="fill-ios-card stroke-red-500" stroke-width="3" />
          </svg>
        </div>
      </div>

      <!-- Shift Stats -->
      <div>
        <h3 class="text-xs text-gray-400 font-bold uppercase tracking-widest mb-4 px-2">8 Hour Statistics</h3>
        <div class="grid grid-cols-2 gap-4">
          <div class="bg-ios-card p-5 rounded-[1.8rem] shadow-sm">
            <p class="text-gray-400 text-xs font-bold uppercase">Min</p>
            <p class="text-ios-text text-2xl font-bold mt-1">{{ sensorData.min }} <span class="text-sm font-normal text-gray-400">{{ sensorData.unit }}</span></p>
          </div>
          <div class="bg-ios-card p-5 rounded-[1.8rem] shadow-sm">
            <p class="text-gray-400 text-xs font-bold uppercase">Max</p>
            <p class="text-red-500 text-2xl font-bold mt-1">{{ sensorData.max }} <span class="text-sm font-normal text-gray-400">{{ sensorData.unit }}</span></p>
          </div>
        </div>
      </div>

      <!-- Threshold Bar -->
      <div>
        <h3 class="text-xs text-gray-400 font-bold uppercase tracking-widest mb-4 px-2">Safety Zones</h3>
        <div class="h-6 w-full bg-gray-200 dark:bg-white/5 rounded-full overflow-hidden flex relative ring-1 ring-black/5 dark:ring-white/5">
          <!-- Green Zone (20-80%) -->
          <div class="absolute left-[20%] width-[60%] h-full bg-green-500/20 w-[60%] border-l border-r border-green-500/30"></div>
          <!-- Current Value Marker -->
          <div class="absolute top-0 bottom-0 w-1.5 bg-white shadow-[0_0_8px_rgba(0,0,0,0.3)] z-10 transition-all duration-500 rounded-full my-0.5" :style="{ left: '90%' }"></div>
        </div>
        <div class="flex justify-between text-[10px] text-gray-400 mt-2 font-mono px-1">
          <span>0.0</span>
          <span>{{ sensorData.limitLow }}</span>
          <span>{{ sensorData.limitHigh }}</span>
          <span>10.0</span>
        </div>
      </div>
    </div>
  </div>
</template>