import { useState, useEffect } from "react";
import type { MetricWithHistory } from "../hooks/useMetrics";

export const SensorCard = ({ metric }: { metric: MetricWithHistory }) => {
  const [prevValue, setPrevValue] = useState(metric.Value);
  const [flash, setFlash] = useState(false);

  // Определение типа на лету (0/1 обычно DIGITAL)
  const isDigital = metric.Value === 0 || metric.Value === 1;

  if (metric.Value !== prevValue) {
    setPrevValue(metric.Value);
    setFlash(true);
  }

  useEffect(() => {
    if (flash) {
      const timer = setTimeout(() => setFlash(false), 600);
      return () => clearTimeout(timer);
    }
  }, [flash]);

  const generatePath = () => {
    if (metric.history.length < 2) return "";
    const min = Math.min(...metric.history);
    const max = Math.max(...metric.history);
    const range = max - min || 1;
    const width = 100;
    const height = 30;
    
    const points = metric.history.map((val, i) => {
      const x = (i / (metric.history.length - 1)) * width;
      const y = height - ((val - min) / range) * height;
      return `${x},${y}`;
    });
    
    return `M ${points.join(" L ")}`;
  };

  return (
    <div
      className={`relative p-4 rounded-2xl transition-colors duration-300 flex items-center justify-between gap-4 ${
        flash ? "bg-indigo-50/50" : "bg-white"
      }`}
    >
      <div className="flex items-center gap-4 min-w-[120px]">
        <div className="w-10 h-10 rounded-xl bg-[#f5f5f7] flex items-center justify-center text-[#1d1d1f] font-bold text-xs">
          ID{metric.SensorId}
        </div>
        <div>
          <span className="text-[9px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5">
            Статус
          </span>
          <div className="flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            <span className="text-xs font-bold text-[#1d1d1f]">Активен</span>
          </div>
        </div>
      </div>

      <div className="hidden sm:block flex-1 max-w-[120px]">
        {!isDigital && (
          <svg width="100" height="30" className="overflow-visible opacity-40">
            <path
              d={generatePath()}
              fill="none"
              stroke="#6366f1"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        )}
      </div>

      <div className="flex items-center gap-6">
        <div className="text-right">
          <span className="text-[9px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5">
            Значение
          </span>
          <div className="flex items-baseline gap-1 justify-end">
            {isDigital ? (
              <span className={`text-xl font-bold uppercase ${metric.Value === 1 ? "text-emerald-600" : "text-rose-500"}`}>
                {metric.Value === 1 ? "ВКЛ" : "ВЫКЛ"}
              </span>
            ) : (
              <span className="text-xl font-bold tabular-nums text-[#1d1d1f]">
                {metric.Value.toFixed(2)}
              </span>
            )}
          </div>
        </div>
        
        <div className="hidden md:block w-px h-8 bg-[#f5f5f7]" />
        
        <div className="hidden md:block text-right min-w-[70px]">
          <span className="text-[9px] font-bold text-[#86868b] uppercase tracking-wider block mb-0.5">
            Обновлено
          </span>
          <span className="text-[11px] font-medium tabular-nums text-[#86868b]">
            {(() => {
              const d = new Date(metric.Time);
              return `${d.toLocaleTimeString([], { hour12: false })}.${d.getMilliseconds().toString().padStart(3, '0')}`;
            })()}
          </span>
        </div>
      </div>
    </div>
  );
};
