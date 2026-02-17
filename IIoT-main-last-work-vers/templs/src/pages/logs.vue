<script setup lang="ts">
const logs = [
  { id: 1, type: 'critical', message: 'Pressure P-102 exceeded safe limit (8.2 Bar)', time: '10:42:05', unit: 'Unit #1' },
  { id: 2, type: 'warning', message: 'Temperature T-04 approaching limit (78°C)', time: '10:30:12', unit: 'Unit #1' },
  { id: 3, type: 'info', message: 'System backup completed', time: '09:00:00', unit: 'System' },
  { id: 4, type: 'critical', message: 'Connection lost to Modbus Collector', time: '08:45:33', unit: 'Network' },
  { id: 5, type: 'success', message: 'Modbus Collector reconnected', time: '08:46:10', unit: 'Network' },
]
</script>

<template>
  <div class="space-y-6 pb-24">
    <div class="px-1 flex justify-between items-end">
       <div>
        <h2 class="text-3xl font-bold tracking-tight text-ios-text">Logs</h2>
        <p class="text-sm text-gray-500 font-medium mt-1">System Events & Alerts</p>
       </div>
      <button class="text-xs font-bold text-blue-500 bg-blue-500/10 px-4 py-2 rounded-full hover:bg-blue-500/20 transition-colors">Clear</button>
    </div>

    <div class="bg-ios-card rounded-[2rem] shadow-sm overflow-hidden divide-y divide-gray-100 dark:divide-white/5">
      <div v-for="log in logs" :key="log.id" class="p-5 flex gap-4 items-start hover:bg-gray-50 dark:hover:bg-white/5 transition-colors cursor-default">
        <div class="mt-0.5 flex-shrink-0">
          <i v-if="log.type === 'critical'" class="fa-solid fa-circle-exclamation text-red-500 text-xl"></i>
          <i v-else-if="log.type === 'warning'" class="fa-solid fa-triangle-exclamation text-orange-500 text-xl"></i>
          <i v-else-if="log.type === 'success'" class="fa-solid fa-circle-check text-green-500 text-xl"></i>
          <i v-else class="fa-solid fa-circle-info text-blue-500 text-xl"></i>
        </div>
        <div class="flex-1 min-w-0">
          <div class="flex justify-between items-start">
            <p class="text-lg font-semibold text-ios-text leading-tight">{{ log.message }}</p>
            <span class="text-xs font-medium text-gray-400 ml-3 whitespace-nowrap">{{ log.time }}</span>
          </div>
          <div class="mt-2 flex items-center gap-2">
             <span class="text-xs font-medium px-2.5 py-1 rounded-lg bg-gray-100 dark:bg-white/10 text-gray-500">{{ log.unit }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>