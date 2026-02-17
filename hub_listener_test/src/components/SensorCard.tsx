import { useState, useEffect } from "react";
import type { MetricWithHistory } from "./DashBoard";

export const SensorCard = ({ metric }: { metric: MetricWithHistory }) => {
  const [prevValue, setPrevValue] = useState(metric.Value);
  const [flash, setFlash] = useState(false);

  if (metric.Value !== prevValue) {
    setPrevValue(metric.Value);
    setFlash(true);
  }

  useEffect(() => {
    if (flash) {
      const timer = setTimeout(() => setFlash(false), 800);
      return () => clearTimeout(timer);
    }
  }, [flash]);

  // Генерация пути для sparkline
  const generatePath = () => {
    if (metric.history.length < 2) return "";
    const min = Math.min(...metric.history);
    const max = Math.max(...metric.history);
    const range = max - min || 1;
    const width = 120;
    const height = 40;
    
    const points = metric.history.map((val, i) => {
      const x = (i / (metric.history.length - 1)) * width;
      const y = height - ((val - min) / range) * height;
      return `${x},${y}`;
    });
    
    return `M ${points.join(" L ")}`;
  };

  return (
    <div
      className={`group relative p-5 rounded-[24px] border transition-all duration-500 flex items-center justify-between gap-6 ${
        flash 
          ? "bg-emerald-50/50 border-emerald-200 shadow-sm" 
          : "bg-white border-[#d2d2d7]/30 hover:border-[#d2d2d7] hover:shadow-md"
      }`}
    >
      <div className="flex items-center gap-5 min-w-[140px]">
        <div className="w-12 h-12 rounded-2xl bg-[#f5f5f7] flex items-center justify-center text-[#1d1d1f] font-bold text-sm border border-[#d2d2d7]/20">
          #{metric.SensorId}
        </div>
        <div>
          <span className="text-[10px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5">
            Node Status
          </span>
          <div className="flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            <span className="text-sm font-semibold text-[#1d1d1f]">Active</span>
          </div>
        </div>
      </div>

      <div className="hidden sm:block flex-1 max-w-[160px]">
        <svg width="120" height="40" className="overflow-visible">
          <path
            d={generatePath()}
            fill="none"
            stroke={flash ? "#10b981" : "#6366f1"}
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="transition-all duration-500"
          />
        </svg>
      </div>

      <div className="flex items-center gap-8">
        <div className="text-right">
          <span className="text-[10px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5 text-right">
            Current Value
          </span>
          <div className="flex items-baseline gap-1 justify-end">
            <span className={`text-2xl font-bold tracking-tight transition-colors duration-300 ${flash ? "text-emerald-600" : "text-[#1d1d1f]"}`}>
              {metric.Value.toFixed(2)}
            </span>
            <span className="text-[10px] font-bold text-[#86868b]">UNIT</span>
          </div>
        </div>
        
        <div className="hidden md:block w-px h-10 bg-[#d2d2d7]/30" />
        
        <div className="hidden md:block text-right min-w-[80px]">
          <span className="text-[10px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5">
            Updated
          </span>
          <span className="text-sm font-medium text-[#1d1d1f]">
            {new Date(metric.Time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
          </span>
        </div>
      </div>

      {/* Индикатор активности наведения */}
      <div className="absolute left-2 top-1/2 -translate-y-1/2 w-1 h-8 rounded-full bg-indigo-500 opacity-0 group-hover:opacity-100 transition-opacity" />
    </div>
  );
};
