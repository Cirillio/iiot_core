import { createRouter, createWebHistory } from 'vue-router'
import DashboardPage from '@/pages/index.vue'
import LogsPage from '@/pages/logs.vue'
import SettingsPage from '@/pages/settings.vue'
import QRScannerPage from '@/pages/qr-scan.vue'
import SystemHealthPage from '@/pages/system-health.vue'
import SensorDetailPage from '@/pages/sensor/[id].vue'
import AssetViewPage from '@/pages/asset/[slug].vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'index',
      component: DashboardPage,
    },
    {
      path: '/logs',
      name: 'logs',
      component: LogsPage,
    },
    {
      path: '/settings',
      name: 'settings',
      component: SettingsPage,
    },
    {
      path: '/qr-scan',
      name: 'qr-scan',
      component: QRScannerPage,
    },
    {
      path: '/system-health',
      name: 'system-health',
      component: SystemHealthPage,
    },
    {
      path: '/sensor/:id',
      name: 'sensor-detail',
      component: SensorDetailPage,
    },
    {
      path: '/asset/:slug',
      name: 'asset-view',
      component: AssetViewPage,
    },
  ],
})

export default router
