<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <div class="space-y-8 pb-24">
    <div class="px-1 flex justify-between items-end">
       <div>
        <h2 class="text-3xl font-bold tracking-tight text-ios-text">Settings</h2>
        <p class="text-sm text-gray-500 font-medium mt-1">Application Configuration</p>
       </div>
    </div>

    <!-- Connection Settings -->
    <section class="space-y-3">
      <h3 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest px-2">Connection</h3>
      <div class="bg-ios-card rounded-[1.5rem] overflow-hidden shadow-sm">
        <div class="p-4 flex justify-between items-center active:bg-gray-50 dark:active:bg-white/5 transition-colors cursor-pointer border-b border-gray-100 dark:border-white/5 last:border-0">
          <div>
            <p class="text-[15px] text-ios-text font-semibold">Gateway URL</p>
            <p class="text-[13px] text-gray-500">api.iiot-system.com</p>
          </div>
          <i class="fa-solid fa-chevron-right text-gray-300 text-xs"></i>
        </div>
        <div class="p-4 flex justify-between items-center">
          <div>
            <p class="text-[15px] text-ios-text font-semibold">Cloudflare Tunnel</p>
            <p class="text-[13px] text-gray-500">Secure connection (Zero Trust)</p>
          </div>
          <!-- iOS Toggle -->
          <div class="w-12 h-7 bg-green-500 rounded-full relative cursor-pointer shadow-inner transition-colors duration-300">
            <div class="w-6 h-6 bg-white rounded-full absolute top-0.5 right-0.5 shadow-md transform transition-transform duration-300"></div>
          </div>
        </div>
      </div>
    </section>

    <!-- Diagnostics -->
    <section class="space-y-3">
      <h3 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest px-2">Diagnostics</h3>
      <div class="bg-ios-card rounded-[1.5rem] overflow-hidden shadow-sm">
        <RouterLink to="/system-health" class="p-4 flex justify-between items-center hover:bg-gray-50 dark:hover:bg-white/5 transition-colors">
          <div class="flex items-center gap-4">
            <div class="w-9 h-9 rounded-xl bg-blue-500/10 flex items-center justify-center text-blue-500">
              <i class="fa-solid fa-heart-pulse"></i>
            </div>
            <div>
              <p class="text-[15px] text-ios-text font-semibold">System Health</p>
              <p class="text-[13px] text-gray-500">Check service status</p>
            </div>
          </div>
          <i class="fa-solid fa-chevron-right text-gray-300 text-xs"></i>
        </RouterLink>
      </div>
    </section>

    <!-- App Info -->
    <section class="space-y-3">
      <h3 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest px-2">About</h3>
      <div class="bg-ios-card rounded-[1.5rem] p-6 text-center shadow-sm">
        <div class="w-20 h-20 bg-gray-100 dark:bg-white/5 rounded-3xl mx-auto mb-4 flex items-center justify-center">
          <i class="fa-solid fa-industry text-3xl text-gray-400"></i>
        </div>
        <h4 class="text-ios-text font-bold text-lg">IIoT Field Monitor</h4>
        <p class="text-sm text-gray-500 mt-1 font-medium">Version 2.0.1 (Build 45)</p>
      </div>
    </section>
    
    <button class="w-full py-4 rounded-[1.5rem] bg-red-500/10 text-red-500 text-[15px] font-bold hover:bg-red-500/20 active:scale-98 transition-all">
      Log Out
    </button>
  </div>
</template>