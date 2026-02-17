import { useMetrics } from "../hooks/useMetrics";
import { SensorCard } from "./SensorCard";
import { ConnectionBadge, StatsCard } from "./dashboard/StatusItems";
import { TelemetryLog } from "./dashboard/TelemetryLog";

const HUB_URL = "http://localhost:5000/hubs/metrics";

export const Dashboard = () => {
  const { metrics, globalHistory, status, lastUpdate, stats } = useMetrics(HUB_URL);

  return (
    <div className="flex flex-col h-screen w-screen bg-[#f5f5f7] overflow-hidden">
      {/* Шапка (фиксированная высота) */}
      <header className="h-16 bg-white flex items-center justify-between px-6 shrink-0 z-30 border-b border-[#e5e5e7]">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center text-white">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <div>
            <h1 className="text-sm font-bold tracking-tight text-[#1d1d1f]">IIoT Hub</h1>
            <p className="text-[8px] text-[#86868b] font-bold uppercase tracking-[0.1em]">Мониторинг телеметрии</p>
          </div>
        </div>
        
        <div className="flex items-center gap-6">
          <div className="hidden lg:flex gap-6 items-center">
             <div className="text-right">
                <p className="text-[8px] font-bold text-[#86868b] uppercase tracking-wider">Датчиков</p>
                <p className="text-xs font-bold">{stats?.count || 0}</p>
             </div>
             <div className="text-right">
                <p className="text-[8px] font-bold text-[#86868b] uppercase tracking-wider">Ср. значение</p>
                <p className="text-xs font-bold text-indigo-600">{stats?.avg || "0.00"}</p>
             </div>
          </div>
          <ConnectionBadge status={status} lastUpdate={lastUpdate} />
        </div>
      </header>

      {/* Основная сетка */}
      <div className="flex-1 flex flex-col md:flex-row overflow-hidden p-4 md:p-5 gap-5">
        
        {/* Панель датчиков (Скролл) */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="flex items-center justify-between mb-3 px-1">
            <h2 className="text-[10px] font-bold text-[#86868b] uppercase tracking-widest">Активные узлы</h2>
            <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
          </div>
          
          <div className="flex-1 overflow-y-auto pr-1">
            {metrics.length === 0 ? (
              <div className="h-full flex items-center justify-center bg-white rounded-2xl">
                 <div className="text-center">
                    <div className="w-8 h-8 border-2 border-indigo-100 border-t-indigo-600 rounded-full animate-spin mx-auto mb-3" />
                    <p className="text-[10px] text-[#86868b] font-bold uppercase tracking-widest">Синхронизация...</p>
                 </div>
              </div>
            ) : (
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-3">
                {metrics
                  .sort((a, b) => a.SensorId - b.SensorId)
                  .map((metric) => (
                    <SensorCard key={metric.SensorId} metric={metric} />
                  ))}
              </div>
            )}
          </div>
        </div>

        {/* Боковая панель (Статистика и Журнал) */}
        <div className="w-full md:w-[380px] 2xl:w-[450px] flex flex-col gap-5 shrink-0 h-full">
          
          {/* Мини-карточки */}
          <div className="grid grid-cols-2 gap-3 shrink-0">
             <div className="bg-white p-4 rounded-2xl">
                <span className="text-[8px] font-bold text-[#86868b] uppercase tracking-wider block mb-1">Статус сети</span>
                <span className="text-sm font-bold">Оптимально</span>
             </div>
             <div className="bg-white p-4 rounded-2xl">
                <span className="text-[8px] font-bold text-[#86868b] uppercase tracking-wider block mb-1">Частота данных</span>
                <span className="text-sm font-bold">1.0 Гц</span>
             </div>
          </div>

          {/* Журнал логов */}
          <TelemetryLog history={globalHistory} />
        </div>

      </div>
    </div>
  );
};
