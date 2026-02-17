<script setup lang="ts">
defineProps<{
  id: string
  title: string
  value: number
  unit: string
  icon: string
  iconColor: string
  barColor: string // class for bg color
  barWidth: string // %
  isCritical?: boolean
}>()
</script>

<template>
  <div 
    class="group relative overflow-hidden rounded-[2rem] bg-ios-card p-8 shadow-sm hover:shadow-xl hover:shadow-black/5 transition-all duration-500 ease-apple cursor-pointer active:scale-95"
    :class="{ 'ring-4 ring-red-500/30 dark:ring-red-400/30': isCritical }"
    @click="$emit('click')"
  >
    <!-- Background Gradient for Critical State -->
    <div v-if="isCritical" class="absolute inset-0 bg-red-50 dark:bg-red-900/10 pointer-events-none transition-colors"></div>
    
    <div class="relative z-10 flex flex-col h-full justify-between gap-6">
      <div class="flex justify-between items-start">
        <div class="flex h-12 w-12 items-center justify-center rounded-2xl bg-gray-50 dark:bg-white/5 transition-colors">
            <i :class="[icon, iconColor, 'text-2xl']"></i>
        </div>
        <span class="rounded-full bg-gray-100 px-3 py-1 text-xs font-bold uppercase tracking-wider text-gray-500 dark:bg-white/5 dark:text-gray-400">{{ id }}</span>
      </div>
      
      <div>
        <p class="text-gray-500 dark:text-gray-400 text-lg font-medium leading-tight mb-1">{{ title }}</p>
        <div class="flex items-baseline gap-2">
            <span class="text-5xl font-extrabold tracking-tight text-ios-text leading-none" :class="isCritical ? 'text-red-600 dark:text-red-400' : ''">
                {{ value }}
            </span>
            <span class="text-lg font-semibold text-gray-400">{{ unit }}</span>
        </div>
      </div>

      <div class="h-3 w-full bg-gray-100 dark:bg-white/5 rounded-full overflow-hidden">
        <div class="h-full rounded-full transition-all duration-700 ease-out shadow-[0_0_10px_rgba(0,0,0,0.1)]" :class="barColor" :style="{ width: barWidth }"></div>
      </div>
    </div>
  </div>
</template>