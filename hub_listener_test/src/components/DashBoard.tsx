import { useMetrics } from "../hooks/useMetrics";
import { SensorCard } from "./SensorCard";
import { ConnectionBadge, StatsCard } from "./dashboard/StatusItems";
import { TelemetryLog } from "./dashboard/TelemetryLog";

const HUB_URL = "http://localhost:5000/hubs/metrics";

export const Dashboard = () => {
  const { metrics, globalHistory, status, lastUpdate, stats } = useMetrics(HUB_URL);

  return (
    <div className="min-h-screen bg-[#fbfbfd] text-[#1d1d1f] font-sans antialiased pb-24">
      <div className="max-w-5xl mx-auto px-6 pt-12">
        {/* Top Header */}
        <header className="mb-14 flex flex-col md:flex-row md:items-end justify-between gap-8">
          <div className="space-y-2">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-50 text-indigo-600 text-[10px] font-bold uppercase tracking-widest">
              Industrial IoT
            </div>
            <h1 className="text-5xl font-semibold tracking-tight text-gray-900">
              Operations Hub
            </h1>
            <p className="text-[#86868b] text-xl font-medium max-w-lg">
              Real-time monitoring and advanced telemetry analysis for connected sensor networks.
            </p>
          </div>
          <ConnectionBadge status={status} lastUpdate={lastUpdate} />
        </header>

        {/* Dynamic Metrics Section */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-16">
          <div className="lg:col-span-2 space-y-4">
            <h2 className="text-xs font-bold text-[#86868b] uppercase tracking-[0.2em] px-2 mb-4">Live Sensor Nodes</h2>
            {metrics.length === 0 ? (
              <div className="h-64 flex items-center justify-center bg-white rounded-[32px] border border-dashed border-[#d2d2d7]">
                 <p className="text-[#86868b] font-medium">Scanning network for active nodes...</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-4">
                {metrics
                  .sort((a, b) => a.SensorId - b.SensorId)
                  .map((metric) => (
                    <SensorCard key={metric.SensorId} metric={metric} />
                  ))}
              </div>
            )}
          </div>

          {/* Sidebar Stats */}
          <div className="space-y-6">
            <h2 className="text-xs font-bold text-[#86868b] uppercase tracking-[0.2em] px-2 mb-4">System Insights</h2>
            <div className="flex flex-col gap-4">
              <StatsCard 
                label="System Load" 
                value={stats?.count || 0} 
                unit="Nodes" 
              />
              <StatsCard 
                label="Average Value" 
                value={stats?.avg || "0.00"} 
                color="text-indigo-600" 
              />
              <div className="bg-emerald-600 p-8 rounded-[32px] text-white shadow-xl shadow-emerald-100 relative overflow-hidden group">
                <div className="relative z-10">
                   <span className="text-[11px] font-bold opacity-70 uppercase tracking-widest">Network Health</span>
                   <p className="text-3xl font-semibold mt-2">Optimal</p>
                   <p className="text-xs mt-4 opacity-80 leading-relaxed font-medium">All active sensors are transmitting within normal latency parameters.</p>
                </div>
                <div className="absolute -right-4 -bottom-4 w-24 h-24 bg-white/10 rounded-full blur-2xl group-hover:scale-150 transition-transform duration-700" />
              </div>
            </div>
          </div>
        </div>

        {/* Full Width Log Feed */}
        <TelemetryLog history={globalHistory} />

        <footer className="mt-24 pt-8 border-t border-[#d2d2d7]/30 text-center">
          <div className="flex justify-center gap-6 mb-4">
             <span className="text-[10px] font-bold text-[#d2d2d7] uppercase tracking-widest cursor-default">Privacy</span>
             <span className="text-[10px] font-bold text-[#d2d2d7] uppercase tracking-widest cursor-default">Compliance</span>
             <span className="text-[10px] font-bold text-[#d2d2d7] uppercase tracking-widest cursor-default">Security</span>
          </div>
          <p className="text-[11px] text-[#86868b] font-medium tracking-tight">
            Designed for high-performance IIoT Environments. © 2026 Hub Systems.
          </p>
        </footer>
      </div>
    </div>
  );
};
