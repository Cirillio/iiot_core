<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const capture = () => {
  // Mock successful scan
  router.replace('/asset/boiler-feed-pump-b')
}
</script>

<template>
  <div class="flex flex-col h-screen bg-black relative">
    <!-- Camera Mock -->
    <div class="flex-1 bg-gray-900 relative flex items-center justify-center overflow-hidden">
      <!-- Camera Feed Placeholder -->
      <div class="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1550751827-4bd374c3f58b?q=80&w=2070&auto=format&fit=crop')] bg-cover bg-center opacity-60"></div>
      
      <!-- Overlay -->
      <div class="absolute inset-0 bg-black/40 z-10"></div>
      
      <!-- Scanner Frame -->
      <div class="relative z-20 w-72 h-72 border border-white/30 rounded-3xl flex flex-col justify-between p-4 backdrop-blur-sm">
        <div class="flex justify-between">
          <div class="w-8 h-8 border-t-4 border-l-4 border-white rounded-tl-xl"></div>
          <div class="w-8 h-8 border-t-4 border-r-4 border-white rounded-tr-xl"></div>
        </div>
        
        <!-- Scanning Line -->
        <div class="absolute top-1/2 left-4 right-4 h-0.5 bg-blue-500 shadow-[0_0_15px_rgba(59,130,246,1)] animate-pulse"></div>
        
        <div class="flex justify-between">
          <div class="w-8 h-8 border-b-4 border-l-4 border-white rounded-bl-xl"></div>
          <div class="w-8 h-8 border-b-4 border-r-4 border-white rounded-br-xl"></div>
        </div>
      </div>

      <div class="absolute bottom-32 z-20 text-white text-center w-full px-10">
        <p class="text-sm font-medium bg-black/60 px-6 py-3 rounded-full backdrop-blur-md inline-block">Align QR code within frame</p>
      </div>
    </div>

    <!-- Controls -->
    <div class="bg-black/90 backdrop-blur-xl pb-safe pt-6 px-8 flex justify-between items-center h-32">
      <button @click="router.back()" class="w-12 h-12 rounded-full bg-white/10 flex items-center justify-center text-white hover:bg-white/20 transition-all active:scale-95">
        <i class="fa-solid fa-xmark text-lg"></i>
      </button>
      
      <button @click="capture" class="w-20 h-20 rounded-full border-4 border-white/30 flex items-center justify-center bg-transparent active:scale-95 transition-all">
        <div class="w-16 h-16 bg-white rounded-full shadow-[0_0_15px_rgba(255,255,255,0.5)]"></div>
      </button>
      
      <button class="w-12 h-12 rounded-full bg-white/10 flex items-center justify-center text-yellow-400 hover:bg-white/20 transition-all active:scale-95">
        <i class="fa-solid fa-bolt text-lg"></i>
      </button>
    </div>
  </div>
</template>